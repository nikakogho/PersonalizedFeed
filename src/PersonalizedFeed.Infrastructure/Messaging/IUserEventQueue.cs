using PersonalizedFeed.Domain.Events;

namespace PersonalizedFeed.Infrastructure.Messaging;

public interface IUserEventQueue
{
    Task EnqueueAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<UserEventBatch> DequeueAsync(
        CancellationToken cancellationToken = default);
}
