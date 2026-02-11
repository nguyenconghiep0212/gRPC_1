using DevicesSimulator;
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

var heartbeatStates = new ConcurrentDictionary<string, HeartbeatSession>();

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
			await CommandRedirect(deviceId, cmd, client);
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

async Task CommandRedirect(string deviceId, IotGrpcLearning.Proto.Command cmd, DeviceGateway.DeviceGatewayClient client)
{
	var args = cmd.Args.Count == 0 ? "{}" : "{" + string.Join(", ", cmd.Args.Select(kv => $"{kv.Key}={kv.Value}")) + "}";
	Console.WriteLine($"[Commands] Received: Device={deviceId} cmdName={cmd.Name} args={args}");

	// Heartbeat Command
	using CancellationTokenSource heartbeat_cts = new CancellationTokenSource();
	CancellationToken ct = heartbeat_cts.Token;
	var heartbeat = client.Heartbeat(cancellationToken: heartbeat_cts.Token);
	if (cmd.Name == "StartHeartbeat")
	{

		await StartHeartbeatSessionAsync(deviceId, client);
		return;
	}
	if (cmd.Name == "StopHeartbeat")
	{
		await StopHeartbeatSessionAsync(deviceId);
		return;
	}
}

// 4) Bi-di Heartbeat
#region [HEARTBEAT]

async Task StartHeartbeatSessionAsync(string deviceId, DeviceGateway.DeviceGatewayClient client)
{
    if (heartbeatStates.ContainsKey(deviceId))
    {
        Console.WriteLine($"[Commands] Heartbeat already running for {deviceId}");
        return;
    }

    var cts = new CancellationTokenSource();
    var call = client.Heartbeat(cancellationToken: cts.Token);

    var sendTask = StartHeartBeat(deviceId, call, cts);
    var readTask = ReadHeartbeatAnalyzedStatus(deviceId, call, cts);

    var session = new HeartbeatSession
    {
        Cts = cts,
        Call = call,
        SendTask = sendTask,
        ReadTask = readTask
    };

    if (!heartbeatStates.TryAdd(deviceId, session))
    {
        // If we lost the race, tear down what we created.
        cts.Cancel();
        call.Dispose();
        cts.Dispose();
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
            Console.WriteLine($"[Device:{deviceId}] Heartbeat cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Device:{deviceId}] Heartbeat error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"[Device:{deviceId}] Heartbeat tasks ended.");
        }
    });
}

async Task StartHeartBeat(string deviceId, AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> heartbeat, CancellationTokenSource cts)
{
	CancellationToken ct = cts.Token;
	int heartbeatInterval = 2; // seconds
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
				await StopHeartbeatSessionAsync(deviceId);
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
async Task StopHeartbeatSessionAsync(string deviceId)
{

	if (!heartbeatStates.TryRemove(deviceId, out var session))
	{
		Console.WriteLine($"[Device:{deviceId}] No active heartbeat to stop.");
		return;
	}

	Console.WriteLine($"[Device:{deviceId}] Stopping heartbeat...");

	try
	{
		// Stop local loops first (prevents writes after completion)
		session.Cts.Cancel();

		// Optional: try to complete request stream gracefully.
		// If the call is already cancelled this may throw; safe to ignore.
		try { await session.Call.RequestStream.CompleteAsync(); } catch { }

		// Wait for tasks to end (but don’t hang forever)
		var all = Task.WhenAll(session.SendTask, session.ReadTask);
		var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(3)));
		if (finished != all)
			Console.WriteLine($"[Device:{deviceId}] Stop timed out; disposing call.");
	}
	finally
	{
		session.Call.Dispose();
		session.Cts.Dispose();
		Console.WriteLine($"[Device:{deviceId}] Heartbeat stopped.");
	}
}
#endregion

ParseResult parseResult = root.Parse(args);
return parseResult.Invoke();