using Grpc.Core;
using IotGrpcLearning.Proto;

namespace DevicesSimulator
{
	sealed class HeartbeatSession
	{
		public required CancellationTokenSource Cts { get; init; }
		public required AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> Call { get; init; }
		public required Task SendTask { get; init; }
		public required Task ReadTask { get; init; }

		public HeartbeatSession(
			CancellationTokenSource cts,
			AsyncDuplexStreamingCall<DeviceStatusRequest, DeviceStatusResponse> call,
			Task sendTask,
			Task readTask)
		{
			Cts = cts;
			Call = call;
			SendTask = sendTask;
			ReadTask = readTask;
		}
		public HeartbeatSession() { }
	}
}
