using PersonalizedFeed.Domain.Events;
using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Domain.Services;

public sealed class UserEventIngestionService : IUserEventIngestionService
{
    private readonly IUserSignalsRepository _userSignalsRepository;
    private readonly IVideoRepository _videoRepository;

    public UserEventIngestionService(
        IUserSignalsRepository userSignalsRepository,
        IVideoRepository videoRepository)
    {
        _userSignalsRepository = userSignalsRepository;
        _videoRepository = videoRepository;
    }

    public async Task IngestAsync(
        string tenantId,
        string userHash,
        IReadOnlyList<UserEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
            return;

        var existing = await _userSignalsRepository
            .GetByTenantAndUserHashAsync(tenantId, userHash, cancellationToken);

        var baseSignals = existing ?? new UserSignals
        {
            TenantId = tenantId,
            UserHash = userHash,
            CategoryStats = new Dictionary<string, CategoryStats>(),
            TotalViewsLast7d = 0,
            TotalWatchTimeLast7dMs = 0,
            SkipRateLast7d = 0,
            LastActiveAt = DateTimeOffset.MinValue,
            UpdatedAt = DateTimeOffset.MinValue
        };

        var categoryStats = baseSignals.CategoryStats.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);

        var distinctVideoIds = events
            .Select(e => e.VideoId)
            .Distinct()
            .ToList();

        var videos = await _videoRepository.GetByIdsAsync(
            tenantId,
            distinctVideoIds,
            cancellationToken);

        var videoIdToMainTag = videos.ToDictionary(v => v.VideoId, v => v.MainTag);

        var lastActiveAt = baseSignals.LastActiveAt;

        foreach (var ev in events)
        {
            if (!videoIdToMainTag.TryGetValue(ev.VideoId, out var mainTag))
            {
                continue;
            }

            if (!categoryStats.TryGetValue(mainTag, out var stats))
            {
                stats = new CategoryStats(Views: 0, WatchTimeMs: 0, Skips: 0);
            }

            switch (ev.Type)
            {
                case UserEventType.VideoView:
                    stats = stats with
                    {
                        Views = stats.Views + 1,
                        WatchTimeMs = stats.WatchTimeMs + (ev.WatchTimeMs ?? 0)
                    };
                    break;

                case UserEventType.Skip:
                    stats = stats with
                    {
                        Skips = stats.Skips + 1
                    };
                    break;

                case UserEventType.Like:
                case UserEventType.Share:
                    break;
            }

            categoryStats[mainTag] = stats;

            if (ev.Timestamp > lastActiveAt)
            {
                lastActiveAt = ev.Timestamp;
            }
        }

        var totalViews = categoryStats.Values.Sum(s => s.Views);
        var totalWatchTime = categoryStats.Values.Sum(s => s.WatchTimeMs);
        var totalSkips = categoryStats.Values.Sum(s => s.Skips);

        var updatedSignals = baseSignals with
        {
            CategoryStats = categoryStats,
            TotalViewsLast7d = totalViews,
            TotalWatchTimeLast7dMs = totalWatchTime,
            SkipRateLast7d = totalViews == 0 ? 0 : (double)totalSkips / totalViews,
            LastActiveAt = lastActiveAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _userSignalsRepository.SaveAsync(updatedSignals, cancellationToken);
    }

    public Task IngestAsync(UserEventBatch batch, CancellationToken cancellationToken = default)
    {
        if (batch.Events.Count == 0)
            return Task.CompletedTask;

        return IngestAsync(batch.TenantId, batch.UserHash, batch.Events, cancellationToken);
    }
}
