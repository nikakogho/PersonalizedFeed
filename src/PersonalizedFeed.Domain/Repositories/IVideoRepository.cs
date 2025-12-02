using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Repositories;

public interface IVideoRepository
{
    Task<IReadOnlyList<Video>> GetCandidateVideosAsync(
        string tenantId,
        int maxCount,
        string maturityPolicy,
        CancellationToken cancellationToken = default);

    Task<Video?> GetByIdAsync(
        string tenantId,
        string videoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Video>> GetByIdsAsync(
        string tenantId,
        IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken = default);
}
