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

		await foreach (var point in requestStream.ReadAllAsync(context.CancellationToken))
		{
			// Basic shape checks
			if (!IsShapeValid(point, out var shapeReason))
			{
				rejected++;
				_logger.LogWarning("Rejected telemetry (shape): device={DeviceId} | reason={Reason}",
					point.DeviceId, shapeReason);
				continue;
			}

			// Extract typed metric/value & validate per type
			if (!TryExtractReading(point, out var metricName, out var numericValue, out var readingReason))
			{
				rejected++;
				_logger.LogWarning("Rejected telemetry (reading): device={DeviceId} | reason={Reason}",
					point.DeviceId, readingReason);
				continue;
			}

			// Accepted: log and (later) persist
			var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(point.UnixMs).UtcDateTime;
			_logger.LogInformation("Telemetry: device={DeviceId} | {Metric}={Value} | at={Ts:dd/MM/yyyy HH:mm:ss.fff}Z",
				point.DeviceId, metricName, numericValue, tsUtc);

			// TODO: persist to DB here (typed columns or JSON payload)
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



	// ================= helpers =================
	private static bool IsShapeValid(TelemetryPoint p, out string reason)
	{
		if (string.IsNullOrWhiteSpace(p.DeviceId))
		{
			reason = "device_id empty";
			return false;
		}

		if (p.UnixMs <= 0)
		{
			reason = "timestamp invalid";
			return false;
		}

		if (p.ReadingCase == TelemetryPoint.ReadingOneofCase.None)
		{
			reason = "no reading set";
			return false;
		}

		reason = "";
		return true;
	}

	/// <summary>
	/// Extracts the typed reading (metric name + numeric value) and validates the value per type.
	/// </summary>
	private static bool TryExtractReading(
		TelemetryPoint p,
		out string metricName,
		out double value,
		out string reason)
	{
		metricName = "";
		value = 0d;
		reason = "";

		switch (p.ReadingCase)
		{
			case TelemetryPoint.ReadingOneofCase.Temperature:
				metricName = "temperature";
				value = p.Temperature?.Celsius ?? double.NaN;
				if (double.IsNaN(value) || double.IsInfinity(value))
				{
					reason = "temperature NaN/Infinity";
					return false;
				}
				// Optional domain sanity (adjust as needed)
				// if (value < -100 || value > 250) { reason = "temperature out of range"; return false; }
				return true;

			case TelemetryPoint.ReadingOneofCase.Rpm:
				metricName = "rpm";
				value = p.Rpm?.Value ?? double.NaN;
				if (double.IsNaN(value) || double.IsInfinity(value))
				{
					reason = "rpm NaN/Infinity";
					return false;
				}
				// if (value < 0 || value > 100000) { reason = "rpm out of range"; return false; }
				return true;

			case TelemetryPoint.ReadingOneofCase.Vibration:
				metricName = "vibration";
				value = p.Vibration?.MmPerS ?? double.NaN;
				if (double.IsNaN(value) || double.IsInfinity(value))
				{
					reason = "vibration NaN/Infinity";
					return false;
				}
				// if (value < 0) { reason = "vibration negative"; return false; }
				return true;

			default:
				reason = "unsupported reading";
				return false;
		}
	}

}
