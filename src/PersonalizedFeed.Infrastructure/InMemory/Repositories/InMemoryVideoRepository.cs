using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Policies;
using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Infrastructure.InMemory.Repositories;

public sealed class InMemoryVideoRepository : IVideoRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryVideoRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Video>> GetCandidateVideosAsync(
        string tenantId,
        int maxCount,
        string maturityPolicy,
        CancellationToken cancellationToken = default)
    {
        var videos = _store.Videos.Values
            .Where(v => v.TenantId == tenantId && v.IsActive && MaturityRatingPolicy.IsAllowed(v.MaturityRating, maturityPolicy))
            .OrderByDescending(v => v.GlobalPopularityScore)
            .ThenByDescending(v => v.CreatedAt)
            .Take(maxCount)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<Video>>(videos);
    }

    public Task<Video?> GetByIdAsync(string tenantId, string videoId, CancellationToken cancellationToken = default)
    {
        var key = (tenantId, videoId);
        var video = _store.Videos.GetValueOrDefault(key);

        return Task.FromResult(video);
    }

    public Task<IReadOnlyList<Video>> GetByIdsAsync(string tenantId, IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken = default)
    {
        var idSet = videoIds.ToHashSet();

        var videos = _store.Videos
            .Values
            .Where(v => v.TenantId == tenantId && idSet.Contains(v.VideoId))
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<Video>>(videos);
    }
}
