using PersonalizedFeed.Domain.Models;
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
        CancellationToken cancellationToken = default)
    {
        var videos = _store.Videos.Values
            .Where(v => v.TenantId == tenantId && v.IsActive)
            .OrderByDescending(v => v.GlobalPopularityScore)
            .ThenByDescending(v => v.CreatedAt)
            .Take(maxCount)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<Video>>(videos);
    }
}
