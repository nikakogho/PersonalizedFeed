using PersonalizedFeed.Domain.Services.Models;

namespace PersonalizedFeed.Domain.Services;

public interface IFeedService
{
    Task<FeedResult> GetFeedAsync(
        FeedRequest request,
        CancellationToken cancellationToken = default);
}
