
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
var deviceId = Environment.GetEnvironmentVariable("DEVICE_ID") ?? "peg-plant1-line3-robot7";
var fwVersion = "1.0.0";

var reply = await client.SayHelloAsync(new DeviceHelloRequest
{
	DeviceId = deviceId,
	FwVersion = fwVersion
});

Console.WriteLine($"[DeviceSimulator] Server says: {reply.Message} (server time: {reply.ServerUnixMs})");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
