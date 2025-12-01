using PersonalizedFeed.Domain.Events;
using PersonalizedFeed.Infrastructure.Messaging;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace PersonalizedFeed.Infrastructure.InMemory;

public sealed class InMemoryUserEventQueue : IUserEventQueue
{
    private readonly Channel<UserEventBatch> _channel;

    public InMemoryUserEventQueue()
    {
        _channel = Channel.CreateUnbounded<UserEventBatch>(
            new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false
            });
    }

    public Task EnqueueAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(batch, cancellationToken).AsTask();
    }

    public async IAsyncEnumerable<UserEventBatch> DequeueAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }
}
