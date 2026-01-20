
using Grpc.Net.Client;
using IotGrpcLearning;

// IMPORTANT: In dev, the gRPC server template uses HTTPS with a dev certificate.
// We'll assume it runs at https://localhost:7096 (check your launchSettings.json).
var serverAddress = "https://localhost:7096";

// Create the gRPC channel
using var channel = GrpcChannel.ForAddress(serverAddress);

// Create the client from generated code
var client = new DeviceGateway.DeviceGatewayClient(channel);

// Prepare a simple hello
var deviceId = Environment.GetEnvironmentVariable("DEVICE_ID") ?? "Station 1";
var fwVersion = "1.0.0";

// Run sequence
await InitAsync();
await Telemetry();
//

async Task InitAsync()
{
	var reply = await client.InitAsync(new DeviceInitRequest
	{
		DeviceId = deviceId,
		FwVersion = fwVersion
	});
	Console.WriteLine($"[DeviceSimulator] Server says: {reply.Message} (server time: {reply.ServerUnixMs})");
}


// 2) SendTelemetry(client streaming)
async Task Telemetry()
{

	using var call = client.SendTelemetry();

	var now = DateTime.UtcNow.Millisecond;

	// A few sample points
	var points = new[]
	{
	new TelemetryPoint { DeviceId = deviceId, Metric = "temperature", Value = 36.7, UnixMs = now },
	new TelemetryPoint { DeviceId = deviceId, Metric = "temperature", Value = 36.9, UnixMs = now + 1000 },
	new TelemetryPoint { DeviceId = deviceId, Metric = "rpm",         Value = 1500,  UnixMs = now + 2000 },
	new TelemetryPoint { DeviceId = deviceId, Metric = "",            Value = double.NaN, UnixMs = 0 } // invalid on purpose
};
	foreach (var p in points)
	{
		await call.RequestStream.WriteAsync(p);
	}
	await call.RequestStream.CompleteAsync();
	var ack = await call.ResponseAsync;

	Console.WriteLine($"[DeviceSimulator] Server says: Accepted: {ack.Accepted}, Rejected: {ack.Rejected}, Note: {ack.Note} ");
}


Console.WriteLine("Press any key to exit...");
Console.ReadKey();
