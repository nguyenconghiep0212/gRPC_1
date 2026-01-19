
using Grpc.Core;
using IotGrpcLearning;

namespace IotGrpcLearning;

public class DeviceGatewayService : DeviceGateway.DeviceGatewayBase
{
	private readonly ILogger<DeviceGatewayService> _logger;

	public DeviceGatewayService(ILogger<DeviceGatewayService> logger)
	{
		_logger = logger;
	}

	public override Task<DeviceHelloResponse> SayHello(DeviceHelloRequest request, ServerCallContext context)
	{
		// Basic guardrails (simple and readable)
		var deviceId = (request.DeviceId ?? string.Empty).Trim();
		var fw = (request.FwVersion ?? string.Empty).Trim();

		if (string.IsNullOrWhiteSpace(deviceId))
		{
			throw new RpcException(new Status(StatusCode.InvalidArgument, "device_id is required"));
		}

		_logger.LogInformation("Device hello: {DeviceId} fw={Fw}", deviceId, string.IsNullOrEmpty(fw) ? "n/a" : fw);

		var reply = new DeviceHelloResponse
		{
			Message = $"Welcome {deviceId}! Gateway online.",
			ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
		};

		return Task.FromResult(reply);
	}

	public override async Task<TelemetryAck> SendTelemetry(
			IAsyncStreamReader<TelemetryPoint> requestStream,
			ServerCallContext context)
	{
		int accepted = 0, rejected = 0;
		string? deviceIdInferred = null;

		await foreach (var point in requestStream.ReadAllAsync(context.CancellationToken))
		{
			// simple validation
			if (string.IsNullOrWhiteSpace(point.DeviceId) ||
				string.IsNullOrWhiteSpace(point.Metric) ||
				double.IsNaN(point.Value) || double.IsInfinity(point.Value))
			{
				rejected++;
				_logger.LogWarning("Rejected telemetry: device={DeviceId} metric={Metric} value={Value}",
					point.DeviceId, point.Metric, point.Value);
				continue;
			}

			deviceIdInferred ??= point.DeviceId;

			// For now, just log; persistence comes later.
			_logger.LogInformation("Telemetry: device={DeviceId} {Metric}={Value} at={Ts}",
				point.DeviceId, point.Metric, point.Value, point.UnixMs);

			accepted++;
		}

		var note = rejected == 0 ? "ok" : "some points were invalid";
		return new TelemetryAck { Accepted = accepted, Rejected = rejected, Note = note };
	}

}
