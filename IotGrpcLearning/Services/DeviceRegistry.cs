
using System.Collections.Concurrent;
using IotGrpcLearning.Proto;

namespace IotGrpcLearning.Services
{
	public sealed class DeviceSnapshot
	{
		public required string DeviceId { get; init; }
		public string Health { get; set; } = "UNKNOWN";
		public string? Details { get; set; }
		public long LastSeenUnixMs { get; set; }
		public bool Connected { get; set; }
		public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	}

	public interface IDeviceRegistry
	{
		void MarkConnected(string deviceId);
		void MarkDisconnected(string deviceId);
		void UpdateStatus(DeviceStatusResponse status);
		IReadOnlyCollection<DeviceSnapshot> GetAll();
		DeviceSnapshot? Get(string deviceId);
	}

	public sealed class DeviceRegistry : IDeviceRegistry
	{
		private readonly ConcurrentDictionary<string, DeviceSnapshot> _devices = new(StringComparer.OrdinalIgnoreCase);
		public void MarkConnected(string deviceId)
		{
			var snap = _devices.GetOrAdd(deviceId, id => new DeviceSnapshot
			{
				DeviceId = id,
				LastSeenUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
			});

			snap.Connected = true;
			snap.LastSeenUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}

		public void MarkDisconnected(string deviceId)
		{
			if (_devices.TryGetValue(deviceId, out var snap))
			{
				snap.Connected = false;
				snap.LastSeenUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			}
		}

		public void UpdateStatus(DeviceStatusResponse status)
		{
			var id = (status.DeviceId ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(id)) return;

			var snap = _devices.GetOrAdd(id, _ => new DeviceSnapshot { DeviceId = id });

			snap.Health = string.IsNullOrWhiteSpace(status.Health) ? "UNKNOWN" : status.Health!;
			snap.Details = status.Details;
			snap.LastSeenUnixMs = status.UnixMs > 0 ? status.UnixMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			snap.Connected = true;
		}

		public IReadOnlyCollection<DeviceSnapshot> GetAll() => _devices.Values.ToArray();

		public DeviceSnapshot? Get(string deviceId)
			=> _devices.TryGetValue(deviceId, out var snap) ? snap : null;
	}

}
