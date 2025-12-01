using PersonalizedFeed.Domain.Events;
using PersonalizedFeed.Domain.Services;

namespace PersonalizedFeed.Api.Messaging;

public sealed class InlineUserEventSink : IUserEventSink
{
    private readonly IUserEventIngestionService _ingestionService;

    public InlineUserEventSink(IUserEventIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public Task HandleAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        return _ingestionService.IngestAsync(batch, cancellationToken);
    }
}
