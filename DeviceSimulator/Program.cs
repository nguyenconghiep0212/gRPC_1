using Grpc.Core;
using Grpc.Net.Client;
using IotGrpcLearning.Proto;
using Microsoft.Extensions.Logging;
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

ParseResult parseResult = root.Parse(args);
return parseResult.Invoke();


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
	using CancellationTokenSource heartbeat_cts = new CancellationTokenSource();

	if (cmd.Name == "StartHeartbeat")
	{
		await StartHeartBeat(deviceId, client, heartbeat_cts.Token, true);
	}
	if (cmd.Name == "StopHeartbeat")
	{
		await StartHeartBeat(deviceId, client, heartbeat_cts.Token, false);
	}
}

// 4) Bi-di Heartbeat
async Task StartHeartBeat(string deviceId, DeviceGateway.DeviceGatewayClient client, CancellationToken ct, bool toggle)
{
	Console.WriteLine("Running HeartBeat...");

	var hb = client.Heartbeat(cancellationToken: ct);
	int heartbeatInterval = 5; // seconds

	if (toggle)
	{
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
				Console.WriteLine($"[Device:{deviceId}] Sending HeartBeat: temp={status.Temperature:F1} at {status.UnixMs}");
				await hb.RequestStream.WriteAsync(status);
			}
		}
		catch (OperationCanceledException error)
		{
			Console.WriteLine($"[Device:{deviceId}] HB write cancelled: {error.Message}");
		}
		// Read responses from server
		try
		{
			while (!ct.IsCancellationRequested)
			{
				await foreach (DeviceStatusResponse res in hb.ResponseStream.ReadAllAsync())
				{
					Console.WriteLine("Device status: {Health}, advice from server: {Detail} at {Ms}", res.Health, res.Details, res.UnixMs);
				}
			}
		}
		catch (OperationCanceledException error)
		{
			Console.WriteLine($"[Device:{deviceId}] HB read cancelled: {error.Message}");
		}
	}
	else
	{
		// End request stream gracefully, wait for reader, dispose
		try { await hb.RequestStream.CompleteAsync(); } catch { }
		hb.Dispose();
	}

	static double PickHealth(Random r)
	{
		return r.Next(100);
	}

}
