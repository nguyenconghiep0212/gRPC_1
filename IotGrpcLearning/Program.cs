using IotGrpcLearning.Proto;
using IotGrpcLearning.Services;
using Microsoft.AspNetCore.Mvc;

namespace IotGrpcLearning
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services.AddGrpc();
			builder.Services.AddSingleton<ICommandBus, InMemoryCommandBus>();

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			app.MapGrpcService<DeviceGatewayService>();
			app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

			// Feed Commands Helper
			app.MapPost("/cmd/{deviceId}/{name}", async (string deviceId, string name, ICommandBus bus, HttpContext http) =>
			{
				Console.WriteLine($"Received command for device '{deviceId}' - '{name}'");
				var cmd = new Command
				{
					CommandId = Guid.NewGuid().ToString("N"),
					Name = name
				};

				if (cmd.Name == "SetThreshold")
				{
					// collect query string as args, e.g., ?key=value
					foreach (var (k, v) in http.Request.Query)
					{
						if (!string.IsNullOrWhiteSpace(k) && v.Count > 0)
							cmd.Args[k] = v[0]!;
					}
					await bus.EnqueueCommandAsync(deviceId, cmd, http.RequestAborted);
					return Results.Ok(new { queued = true, deviceId, cmd = new { cmd.CommandId, cmd.Name, Args = cmd.Args } });
				}
				if (cmd.Name == "StartHeartbeat")
				{
					if (http.Request.Query.Count == 0)
					{
						return Results.BadRequest(new { Error = "No parameter!", Query = new { interval = "int" } });
					}
					foreach (var (k, v) in http.Request.Query)
					{
						if (k != "interval")
						{
							return Results.BadRequest(new { Error = "Incorrect parameter!", Query = new { interval = "int" } });
						}
						if (!string.IsNullOrWhiteSpace(k) && v.Count > 0)
						{
							cmd.Args[k] = v[0]!;
						}
					}
					await bus.EnqueueCommandAsync(deviceId, cmd, http.RequestAborted);
					return Results.Ok(new { queued = true, deviceId, cmd = new { cmd.CommandId, cmd.Name, Args = cmd.Args } });
				}
				if (cmd.Name == "StopHeartbeat")
				{
					await bus.EnqueueCommandAsync(deviceId, cmd, http.RequestAborted);
					return Results.Ok(new { queued = true, deviceId, cmd = new { cmd.CommandId, cmd.Name, Args = cmd.Args } });
				}
				return Results.BadRequest(new { Error = "Incorrect command!" });
			});
			//curl - X POST "https://localhost:7096/cmd/station-1/SetThreshold?metric=temperature&value=38.0"
			
			// Get device-channel mapping
			app.MapGet("/cmd/channeldetail", ([FromServices] ICommandBus bus) =>
			{
				// If the registered implementation is the in-memory one, expose diagnostics
				if (bus is InMemoryCommandBus mem)
				{
					var dict = mem.GetChannelsDictionary();
					var list = dict
						.Select(item => new
						{
							DeviceId = item.Key,
							ReaderCompleted = item.Value.Reader.Completion.IsCompleted
						})
						.OrderBy(x => x.DeviceId)
						.ToArray();

					Console.WriteLine("CommandBus channel snapshot:");
					foreach (var item in list)
					{
						Console.WriteLine($" - {item.DeviceId} (ReaderCompleted={item.ReaderCompleted})");
					}

					return Results.Ok(new { total = dict.Count, channels = list });
				}
				// Non-in-memory implementations: return a safe message
				return Results.Ok(new { total = 0, channels = Array.Empty<object>(), note = "ICommandBus is not InMemoryCommandBus" });
			});

			app.Run();
		}
	}
}
