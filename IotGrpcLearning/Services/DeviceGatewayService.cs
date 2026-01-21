using Grpc.Core;
using IotGrpcLearning.Proto;

namespace IotGrpcLearning.Services;

public class DeviceGatewayService : DeviceGateway.DeviceGatewayBase
{
	private readonly ILogger<DeviceGatewayService> _logger;
	private readonly ICommandBus _commandBus;

	public DeviceGatewayService(ILogger<DeviceGatewayService> logger, ICommandBus commandBus)
	{
		_logger = logger;
		_commandBus = commandBus;
	}

	public override Task<DeviceInitResponse> Init(DeviceInitRequest request, ServerCallContext context)
	{
		// Basic guardrails (simple and readable)
		var deviceId = (request.DeviceId ?? string.Empty).Trim();
		var fw = (request.FwVersion ?? string.Empty).Trim();

		if (string.IsNullOrWhiteSpace(deviceId))
		{
			throw new RpcException(new Status(StatusCode.InvalidArgument, "device_id is required"));
		}

		_logger.LogInformation("Device hello: {DeviceId} fw={Fw}", deviceId, string.IsNullOrEmpty(fw) ? "n/a" : fw);

		var reply = new DeviceInitResponse
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
				_logger.LogWarning("Rejected telemetry: device={DeviceId} | metric={Metric} | value={Value}",
					point.DeviceId, point.Metric, point.Value);
				continue;
			}

			deviceIdInferred ??= point.DeviceId;

			// For now, just log; persistence comes later.
			_logger.LogInformation("Telemetry: device={DeviceId} | {Metric}={Value} | at={Ts}",
				point.DeviceId, point.Metric, point.Value, point.UnixMs.ToString("dd/MM/yyyy HH:mm:ss.fff"));

			accepted++;
		}

		var note = rejected == 0 ? "ok" : "some points were invalid";
		return new TelemetryAck { Accepted = accepted, Rejected = rejected, Note = note };
	}

	public override async Task SubscribeCommands(
		   DeviceId request,
		   IServerStreamWriter<Command> responseStream,
		   ServerCallContext context)
	{
		var deviceId = (request?.Id ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(deviceId))
			throw new RpcException(new Status(StatusCode.InvalidArgument, "device id is required"));

		_logger.LogInformation("Device subscribed for commands: {DeviceId}", deviceId);

		// Subscribe to the device-specific queue
		var reader = _commandBus.Subscribe(deviceId);
		var ct = context.CancellationToken;

		// OPTIONAL: Push a welcome/ping command on subscribe
		await _commandBus.EnqueueAsync(deviceId, new Command
		{
			CommandId = Guid.NewGuid().ToString("N"),
			Name = "Ping",
			Args = { { "reason", "initial-subscribe" } }
		}, ct);

		try
		{
			// Drain commands as they arrive and write them to the stream
			while (await reader.WaitToReadAsync(ct))
			{
				while (reader.TryRead(out var cmd))
				{
					await responseStream.WriteAsync(cmd);
					_logger.LogInformation("Pushed command to {DeviceId}: {Name} ({CommandId})",
						deviceId, cmd.Name, cmd.CommandId);
				}
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Command stream closed for {DeviceId}", deviceId);
		}
	}

}
