using Grpc.Core;
using IotGrpcLearning.Proto;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace IotGrpcLearning.Services;

public interface ICommandBus
{
	ChannelReader<Command> Subscribe(string deviceId);
	ValueTask EnqueueAsync(string deviceId, Command command, CancellationToken ct = default);
}

public sealed class InMemoryCommandBus : ICommandBus
{
	private readonly ConcurrentDictionary<string, Channel<Command>> _channels = new();

	public ChannelReader<Command> Subscribe(string deviceId)
	{
		var channel = _channels.GetOrAdd(deviceId, _ => Channel.CreateUnbounded<Command>());
		return channel.Reader;
	}

	public ValueTask EnqueueAsync(string deviceId, Command command, CancellationToken ct = default)
	{
		var channel = _channels.GetOrAdd(deviceId, _ => Channel.CreateUnbounded<Command>());
		return channel.Writer.WriteAsync(command, ct);
	}
}
