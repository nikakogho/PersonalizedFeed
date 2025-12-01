using PersonalizedFeed.Domain.Events;

namespace PersonalizedFeed.Api.Messaging;

public interface IUserEventSink
{
    Task HandleAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default);
}
