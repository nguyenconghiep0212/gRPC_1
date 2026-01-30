using Grpc.Core;
using IotGrpcLearning.Proto;
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
		return new TelemetryResponse { Accepted = accepted, Rejected = rejected, Note = note };
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

	public override async Task Heartbeat(
			IAsyncStreamReader<DeviceStatusRequest> requestStream,
			IServerStreamWriter<Command> responseStream,
			ServerCallContext context)
	{
		Console.WriteLine("Running HeartBeatAsync...");

		int heartBeatInterval = 3; // seconds
		var ct = context.CancellationToken;

		// We’ll infer deviceId from the first status
		string? deviceId = null;

		// Local outbox to serialize all writes to the response stream
		var outbox = Channel.CreateUnbounded<Command>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});

		// Writer task: the ONLY place that writes to responseStream
		var writerTask = Task.Run(async () =>
		{
			try
			{
				await foreach (var cmd in outbox.Reader.ReadAllAsync(ct))
				{
					await responseStream.WriteAsync(cmd);
					if (deviceId is not null)
					{
						_logger.LogInformation("HB -> {DeviceId}: {Command} ({CmdId})",
							deviceId, cmd.Name, cmd.CommandId);
					}
				}
			}
			catch (OperationCanceledException) { /* normal */ }
		}, ct);

		// Periodic pinger (every 15s)
		using var pingerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var pingerTask = Task.Run(async () =>
		{
			try
			{
				while (!pingerCts.Token.IsCancellationRequested)
				{
					await Task.Delay(TimeSpan.FromSeconds(heartBeatInterval), pingerCts.Token);
					if (deviceId is null) continue;

					await outbox.Writer.WriteAsync(new Command
					{
						CommandId = Guid.NewGuid().ToString("N"),
						Name = "Ping",
						Args = { { "source", "heartbeat-timer" } }
					}, pingerCts.Token);
				}
			}
			catch (OperationCanceledException) { /* normal */ }
		}, pingerCts.Token);

		// OPTIONAL: also pull commands from the global bus and forward into the HB stream
		ChannelReader<Command>? busReader = null;
		Task busPumpTask = Task.CompletedTask;

		try
		{
			await foreach (var status in requestStream.ReadAllAsync(ct))
			{
				deviceId ??= (status.DeviceId ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(deviceId))
				{
					throw new RpcException(new Status(StatusCode.InvalidArgument, "device_id is required in Heartbeat"));
				}

				// lazily attach bus forwarding once deviceId known
				if (busReader is null)
				{
					// Forward /cmd injected commands into this same response stream
					busReader = _commandBus.Subscribe(deviceId);
					busPumpTask = Task.Run(async () =>
					{
						try
						{
							while (await busReader.WaitToReadAsync(ct))
							{
								while (busReader.TryRead(out var cmd))
								{
									await outbox.Writer.WriteAsync(cmd, ct);
								}
							}
						}
						catch (OperationCanceledException) { /* normal */ }
					}, ct);
				}

				var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(status.UnixMs).UtcDateTime;
				_logger.LogInformation("HB <- {DeviceId}: health={Health} details='{Details}' at={Ts:dd/MM/yyyy HH:mm:ss.fff}Z",
					deviceId, status.Health, status.Details ?? "", tsUtc);

				// Reactive rule: CRIT -> immediate EnterSafeMode
				if (string.Equals(status.Health, "CRIT", StringComparison.OrdinalIgnoreCase))
				{
					await outbox.Writer.WriteAsync(new Command
					{
						CommandId = Guid.NewGuid().ToString("N"),
						Name = "EnterSafeMode",
						Args = { { "reason", "critical-health" } }
					}, ct);
				}
				// Optional: WARN -> RequestDiagnostics
				else if (string.Equals(status.Health, "WARN", StringComparison.OrdinalIgnoreCase))
				{
					await outbox.Writer.WriteAsync(new Command
					{
						CommandId = Guid.NewGuid().ToString("N"),
						Name = "RequestDiagnostics",
						Args = { { "reason", "warn-health" } }
					}, ct);
				}
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Heartbeat stream cancelled for {DeviceId}", deviceId ?? "(unknown)");
		}
		finally
		{
			// Stop timers & pumps
			pingerCts.Cancel();
			outbox.Writer.TryComplete();
			try { await pingerTask; } catch { }
			try { await busPumpTask; } catch { }
			try { await writerTask; } catch { }
		}
	}
}
