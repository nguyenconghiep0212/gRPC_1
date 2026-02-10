using Grpc.Core;
using Grpc.Net.Client;
using IotGrpcLearning.Proto;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.CommandLine;

Console.WriteLine($"COUNT env = '{Environment.GetEnvironmentVariable("COUNT") ?? "<null>"}'");
// IMPORTANT: In dev, the gRPC server template uses HTTPS with a dev certificate.
// We'll assume it runs at https://localhost:7096 (check your launchSettings.json).
Option<string> serverOption = new("--server", ["-s"])
{
	Description = "DeviceGateway address",
	DefaultValueFactory = (parseResult) => Environment.GetEnvironmentVariable("SERVER") ?? "https://localhost:7096"
};
Option<int> countOption = new("--count", ["-c"])
{
	Description = "Number of devices to simulate",
	DefaultValueFactory = (parseResult) => int.TryParse(Environment.GetEnvironmentVariable("COUNT"), out var n) ? n : 5
};
Option<string> prefixOption = new("--prefix", ["-p"])
{
	Description = "Device ID prefix",
	DefaultValueFactory = (parseResult) => Environment.GetEnvironmentVariable("PREFIX") ?? "station"
};
Option<int> periodMsOption = new("--period-ms", ["-pms"])
{
	Description = "Telemetry period per device (ms)",
	DefaultValueFactory = (parseResult) => int.TryParse(Environment.GetEnvironmentVariable("PERIOD_MS"), out var p) ? p : 1000
};
Option<string> fwVersionOption = new("--fw-version", ["-fw"])
{
	Description = "Firmware used by devices",
	DefaultValueFactory = (parseResult) => Environment.GetEnvironmentVariable("FWVERSION") ?? "1.0.1"
};

var heartbeatStates = new ConcurrentDictionary<string, (CancellationTokenSource Cts, Task SendTask, Task ReadTask, AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> Call)>();

var root = new RootCommand("Multi Device Simulator");
root.Options.Add(serverOption);
root.Options.Add(countOption);
root.Options.Add(prefixOption);
root.Options.Add(periodMsOption);
root.Options.Add(fwVersionOption);

root.SetAction(async (parseResult) =>
{
	string server = parseResult.GetValue(serverOption) ?? "https://localhost:7096";
	int count = parseResult.GetValue(countOption);
	string prefix = parseResult.GetValue(prefixOption) ?? "station";
	int periodMs = parseResult.GetValue(periodMsOption);
	string fwversion = parseResult.GetValue(fwVersionOption) ?? "1.0.1";
	Console.WriteLine($"[MultiSim] Server={server}, Devices={count}, Prefix={prefix}, Period={periodMs}ms, FW-Version={fwversion}");

	using var channel = GrpcChannel.ForAddress(server);
	var client = new DeviceGateway.DeviceGatewayClient(channel);

	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
	Console.WriteLine("Press ENTER to stop all devices...");
	_ = Task.Run(() => { Console.ReadLine(); cts.Cancel(); });

	// Start N devices
	var tasks = Enumerable.Range(1, count)
		.Select(i => RunDeviceAsync(client, $"{prefix}-{i}", periodMs, fwversion, cts.Token))
		.ToArray();

	await Task.WhenAll(tasks);

	Console.WriteLine("All devices stopped. Press any key to exit...");
	Console.ReadKey();

});

// =========== per-device logic ===========
async Task RunDeviceAsync(DeviceGateway.DeviceGatewayClient client, string deviceId, int periodMs, string fwVersion, CancellationToken ct)
{
	// Arrange

	// Run sequence
	await InitAsync(deviceId, fwVersion, client);
	await Telemetry(deviceId, client);
	await StartSubscribeCommands(deviceId, client);
	//
}

async Task InitAsync(string deviceId, string fwVersion, DeviceGateway.DeviceGatewayClient client)
{
	var reply = await client.InitAsync(new DeviceInitRequest
	{
		DeviceId = deviceId,
		FwVersion = fwVersion
	});
	Console.WriteLine($"[DeviceSimulator] Server says: {reply.Message} (server time: {reply.ServerUnixMs})");
}

// 2) SendTelemetry(client streaming)
async Task Telemetry(string deviceId, DeviceGateway.DeviceGatewayClient client)
{

	using var call = client.SendTelemetry();

	var now = DateTime.UtcNow.Millisecond;

	// A few sample points
	var points = new[]
	{
	new TelemetryRequest { DeviceId = deviceId, Tempature= 36.8, UnixMs = now },
	new TelemetryRequest { DeviceId = deviceId, Tempature= 52, UnixMs = now + 1000 },
	new TelemetryRequest { DeviceId = deviceId, Tempature= 99.9,  UnixMs = now + 2000 },
	//new TelemetryRequest { DeviceId = deviceId, Tempature=null ,            Value = double.NaN, UnixMs = 0 } // invalid on purpose
};
	foreach (var p in points)
	{
		await call.RequestStream.WriteAsync(p);
	}
	await call.RequestStream.CompleteAsync();
	var ack = await call.ResponseAsync;

	Console.WriteLine($"[DeviceSimulator] Server says: Accepted: {ack.Accepted}, Rejected: {ack.Rejected}, Note: {ack.Note} ");
}

// 3) Start server-streaming subscription
async Task StartSubscribeCommands(string deviceId, DeviceGateway.DeviceGatewayClient client)
{
	using CancellationTokenSource cts = new CancellationTokenSource();

	Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

	Console.WriteLine("[Commands] Subscribing to commands... (press ENTER or Ctrl+C to quit)");
	var call = client.SubscribeCommands(new DeviceId { Id = deviceId }, cancellationToken: cts.Token);

	// Read on the main thread to keep scope alive (simplest and safest)
	try
	{
		await foreach (var cmd in call.ResponseStream.ReadAllAsync(cts.Token))
		{
			CommandRedirect(deviceId, cmd, client);
		}
	}
	catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
	{
		Console.WriteLine("[Commands] Stream cancelled (RpcException.Cancelled).");
	}
	catch (OperationCanceledException)
	{
		Console.WriteLine("[Commands] Stream cancelled (OperationCanceledException).");
	}
	finally
	{
		// Ensure the call is disposed AFTER the reader stops
		Console.WriteLine("Server streaming call dispose!");
		call.Dispose();
	}
}

async void CommandRedirect(string deviceId, IotGrpcLearning.Proto.Command cmd, DeviceGateway.DeviceGatewayClient client)
{
	var args = cmd.Args.Count == 0 ? "{}" : "{" + string.Join(", ", cmd.Args.Select(kv => $"{kv.Key}={kv.Value}")) + "}";
	Console.WriteLine($"[Commands] Received: Device={deviceId} cmdName={cmd.Name} args={args}");

	// Heartbeat Command
	using CancellationTokenSource heartbeat_cts = new CancellationTokenSource();
	CancellationToken ct = heartbeat_cts.Token;
	var heartbeat = client.Heartbeat(cancellationToken: heartbeat_cts.Token);
	if (cmd.Name == "StartHeartbeat")
	{
		// If already running, ignore or log
		if (heartbeatStates.ContainsKey(deviceId))
		{
			Console.WriteLine($"[Commands] Heartbeat already running for {deviceId}");
			return;
		}

		// Start both tasks concurrently
		var sendTask = StartHeartBeat(deviceId, heartbeat, heartbeat_cts);
		var readTask = ReadHeartbeatAnalyzedStatus(deviceId, heartbeat, heartbeat_cts);

		if (!heartbeatStates.TryAdd(deviceId, (heartbeat_cts, sendTask, readTask, heartbeat)))
		{
			// unlikely, but ensure we cancel new one if we failed to add
			try { heartbeat_cts.Cancel(); } catch { }
			heartbeat.Dispose();
			return;
		}

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.WhenAll(sendTask, readTask);
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine($"[Debug]|[Device:{deviceId}]: Heartbeat tasks cancelled.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Debug]|[Device:{deviceId}]: Heartbeat tasks error: {ex.Message}");
			}
			finally
			{
				Console.WriteLine($"[Info]|[Device:{deviceId}]: Heartbeat stopped.");
			}
		});
	}
	if (cmd.Name == "StopHeartbeat")
	{
		await StopHeartbeat(deviceId, heartbeat, heartbeat_cts);
	}
}

// 4) Bi-di Heartbeat
#region [HEARTBEAT]
async Task StartHeartBeat(string deviceId, AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> heartbeat, CancellationTokenSource cts)
{
	CancellationToken ct = cts.Token;
	int heartbeatInterval = 21; // seconds
	Console.WriteLine($"[Info]|[Device:{deviceId}]: Sending HeartBeat request...");
	// Send temperature status when get request from server
	var rnd = new Random(deviceId.GetHashCode());
	try
	{
		while (!ct.IsCancellationRequested)
		{
			await Task.Delay(TimeSpan.FromSeconds(heartbeatInterval), ct);
			double tempature = PickHealth(rnd); // "OK" most of the time, sometimes "WARN"/"CRIT"
			var status = new DeviceStatusRequest
			{
				DeviceId = deviceId,
				Temperature = tempature,
				UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
			};
			Console.WriteLine($"[Info]|[Device:{deviceId}]: Sending HeartBeat - temp={status.Temperature:F1} at {status.UnixMs}");
			await heartbeat.RequestStream.WriteAsync(status);
		}
	}
	catch (OperationCanceledException error)
	{
		Console.WriteLine($"[Debug]|[Device:{deviceId}]: HB write cancelled: {error.Message}");
	}
	static double PickHealth(Random r)
	{
		return r.Next(100);
	}
}
async Task ReadHeartbeatAnalyzedStatus(string deviceId, AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> heartbeat, CancellationTokenSource cts)
{
	// Read responses from server
	CancellationToken ct = cts.Token;
	try
	{
		while (await heartbeat.ResponseStream.MoveNext(ct))
		{
			Console.WriteLine($"[Info]|[Device:{deviceId}]: Reading HeartBeat responses...");
			if (ct.IsCancellationRequested)
			{
				Console.WriteLine($"[Info]|[Device:{deviceId}]: Response stream completed by server.");
				break;
			}
			var res = heartbeat.ResponseStream.Current;
			Console.WriteLine($"[Info]|[Device:{deviceId}]: Received server response: Health={res.Health}, Details={res.Details}, UnixMs={res.UnixMs}");
			if (string.Equals(res.Health, "CRIT"))
			{
				await StopHeartbeat(deviceId, heartbeat, cts);
			}
		}
	}
	catch (OperationCanceledException error)
	{
		Console.WriteLine($"[Debug]|[Device:{deviceId}]: HB read cancelled: {error.Message}");
	}
	catch (RpcException rex)
	{
		Console.WriteLine($"[Debug]|[Device:{deviceId}]: HB read RpcException: {rex.Status} - {rex.Message}");
	}
}
async Task StopHeartbeat(string deviceId, AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> heartbeat, CancellationTokenSource heartbeat_cts)
{
	// attempt coordinated shutdown via the shared state
	if (heartbeatStates.TryRemove(deviceId, out var state))
	{
		try
		{
			Console.WriteLine($"[Info]|[Device:{deviceId}]: Stopping Heartbeat - attempting graceful completion...");
			// First try to finish the request stream so server sees a normal EOF.
			try
			{
				// then finish request stream so server receives normal EOF
				try { await state.Call.RequestStream.CompleteAsync(); } catch (Exception e) { Console.WriteLine($"[Debug]|[Device:{deviceId}]: {e.Message}"); }
				// signal cancellation so sender/reader stop
				//heartbeat_cts.Cancel();
				// wait for tasks to finish (they observe the token)
				await Task.WhenAll(new[] { state.SendTask, state.ReadTask }.Where(t => t != null));
				Console.WriteLine($"[Info]|[Device:{deviceId}] RequestStream.CompleteAsync() succeeded.");
			}
			catch (RpcException rex)
			{
				Console.WriteLine($"[Debug]|[Device:{deviceId}]: RequestStream.CompleteAsync() RpcException: {rex.Status} - {rex.Message}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Debug]|[Device:{deviceId}]: RequestStream.CompleteAsync() failed: {ex.Message}");
			}

			// Then cancel local work / readers to stop loops that write to the stream.
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Debug]|[Device:{deviceId}]: Error stopping heartbeat: {ex.Message}");
		}
		finally
		{
			try { heartbeat.Dispose(); } catch (Exception ex) { Console.WriteLine($"[Debug]|[Device:{deviceId}]: StreamingCall dispose fail: {ex.Message} "); }
			try { heartbeat_cts.Dispose(); } catch (Exception ex) { Console.WriteLine($"[Debug]|[Device:{deviceId}]: cancel token dispose fail: {ex.Message} "); }
			Console.WriteLine($"[Info]|[Device:{deviceId}]: Heartbeat stopped");
		}
	}
	else
	{
		Console.WriteLine($"[Info]|[Device:{deviceId}]: No active heartbeat to stop.");
	}
}
#endregion

ParseResult parseResult = root.Parse(args);
return parseResult.Invoke();