using PersonalizedFeed.Domain.Events;

namespace PersonalizedFeed.Domain.Services;

public interface IUserEventIngestionService
{
    Task IngestAsync(
        string tenantId,
        string userHash,
        IReadOnlyList<UserEvent> events,
        CancellationToken cancellationToken = default);

    Task IngestAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default);
}
