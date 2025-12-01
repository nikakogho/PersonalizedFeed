using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Repositories;

public interface IVideoRepository
{
    Task<IReadOnlyList<Video>> GetCandidateVideosAsync(
        string tenantId,
        int maxCount,
        CancellationToken cancellationToken = default);
}
