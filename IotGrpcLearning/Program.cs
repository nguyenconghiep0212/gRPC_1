using IotGrpcLearning.Proto;
using IotGrpcLearning.Services;
using Microsoft.Extensions.Hosting;

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
				var cmd = new Command
				{
					CommandId = Guid.NewGuid().ToString("N"),
					Name = name
				};

				// collect query string as args, e.g., ?key=value
				foreach (var (k, v) in http.Request.Query)
				{
 					if (!string.IsNullOrWhiteSpace(k) && v.Count > 0)
						cmd.Args[k] = v[0]!;
				}

				await bus.EnqueueAsync(deviceId, cmd, http.RequestAborted);
				return Results.Ok(new { queued = true, deviceId, cmd = new { cmd.CommandId, cmd.Name, Args = cmd.Args } });
			});
			//curl - X POST "https://localhost:7096/cmd/peg-plant1-line3-robot7/SetThreshold?metric=temperature&value=38.0"
			//


			app.Run();
        }
    }
}
