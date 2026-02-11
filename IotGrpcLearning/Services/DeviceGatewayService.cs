using Grpc.Core;
using IotGrpcLearning.Proto;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Channels;

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


	public override async Task<TelemetryResponse> SendTelemetry(
			IAsyncStreamReader<TelemetryRequest> requestStream,
			ServerCallContext context)
	{
		int accepted = 0, rejected = 0;
		string? deviceIdInferred = null;

		await foreach (var point in requestStream.ReadAllAsync(context.CancellationToken))
		{
			// simple validation
			if (string.IsNullOrWhiteSpace(point.DeviceId) ||
				double.IsNaN(point.Tempature) || double.IsInfinity(point.Tempature))
			{
				rejected++;
				_logger.LogWarning("Rejected telemetry: device={DeviceId} | tempature={Value}",
					point.DeviceId, point.Tempature);
				continue;
			}
			deviceIdInferred ??= point.DeviceId;

			// For now, just log; persistence comes later.
			_logger.LogInformation("Telemetry: device={DeviceId} | tempature={Value} | at={Ts}",
				point.DeviceId, point.Tempature, point.UnixMs.ToString("dd/MM/yyyy HH:mm:ss.fff"));

			accepted++;
		}

		var note = rejected == 0 ? "ok" : "some points were invalid";
		return new TelemetryResponse { Accepted = accepted, Rejected = rejected, Note = note };
	}

	public override async Task SubscribeCommands(
		   DeviceId DeviceId,
		   IServerStreamWriter<Command> responseStream,
		   ServerCallContext context)
	{
		var deviceId = (DeviceId?.Id ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(deviceId))
			throw new RpcException(new Status(StatusCode.InvalidArgument, "device id is required"));

		_logger.LogInformation("Device subscribed for commands: {DeviceId}", deviceId);

		// Subscribe to the device-specific queue
		var reader = _commandBus.Subscribe(deviceId);
		var ct = context.CancellationToken;

		// OPTIONAL: Push a welcome/ping command on subscribe
		Command cmd = new Command
		{
			CommandId = Guid.NewGuid().ToString("N"),
			Name = "Ping",
			Args = { { "reason", "initial-subscribe" } }
		};
		await QueueCommand(deviceId, cmd, responseStream, context);
	}

	public async Task QueueCommand(
		   string deviceId,
		   Command newCmd,
		   IServerStreamWriter<Command> responseStream,
		   ServerCallContext context
		)
	{
		var reader = _commandBus.Subscribe(deviceId);
		var ct = context.CancellationToken;

		await _commandBus.EnqueueCommandAsync(deviceId, newCmd, ct);
		try
		{
			// Drain commands as they arrive and write them to the stream
			while (await reader.WaitToReadAsync(ct))
			{
				while (reader.TryRead(out var cmd))
				{
					await responseStream.WriteAsync(cmd);
					_logger.LogInformation("Pushed command to {DeviceId}: {Name}",
						deviceId, cmd.Name);
				}
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Command stream closed for {DeviceId}", deviceId);
		}
	}

	public override async Task Heartbeat(
			IAsyncStreamReader<DeviceStatusRequest> requestStream,
			IServerStreamWriter<DeviceStatusResponse> responseStream,
			ServerCallContext context)
	{
		Console.WriteLine("Running HeartBeatAsync...");

		var ct = context.CancellationToken;

		// We’ll infer deviceId from the first status
		string? deviceId = null;
		try
		{
			//await foreach (var status in requestStream.ReadAllAsync(ct))
			while (!ct.IsCancellationRequested)
			{
				var hasData = await requestStream.MoveNext(ct);
                if (!hasData)
                {
					break;
				}
                var status = requestStream.Current;
				deviceId ??= (status.DeviceId ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(deviceId))
				{
					throw new RpcException(new Status(StatusCode.InvalidArgument, "device_id is required in Heartbeat"));
				}

				var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(status.UnixMs).UtcDateTime;
				_logger.LogInformation("HB <- {DeviceId}: Tempature={Temperature} - Health={Health} at={Ts}",
					deviceId, status.Temperature, HealthAnalyze(status.Temperature), tsUtc);

				// Reactive rule: CRIT -> immediate EnterSafeMode
				if (string.Equals(HealthAnalyze(status.Temperature), "CRIT", StringComparison.OrdinalIgnoreCase))
				{
					DeviceStatusResponse res = new DeviceStatusResponse
					{
						Health = "CRIT",
						Details = "enter-safe-mode",
						UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					};
					await responseStream.WriteAsync(res);
					_logger.LogInformation("HB -> {DeviceId}: Sending CRIT", deviceId);
				}
				// Optional: WARN -> RequestDiagnostics
				else if (string.Equals(HealthAnalyze(status.Temperature), "WARN", StringComparison.OrdinalIgnoreCase))
				{
					DeviceStatusResponse res = new DeviceStatusResponse
					{
						Health = "WARNING",
						Details = "request-diagnostic",
						UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					};
					await responseStream.WriteAsync(res);
					_logger.LogInformation("HB -> {DeviceId}: Sending WARN", deviceId);
				}
			}
		}
		catch (IOException ioEx)
		{
			_logger.LogInformation($"[Device:{deviceId ?? "unknown"}]: Heartbeat client disconnected/reset - {ioEx.Message}");
		}
		catch (Exception ex)
		{
			_logger.LogInformation($"[Device:{deviceId ?? "unknown"}]: Heartbeat stream cancelled - {ex.Message}");
		}

		static string HealthAnalyze(double tempature)
		{
			if (tempature < 65) return "OK";
			if (tempature < 90) return "WARN";
			return "CRIT";
		}
	}
}
