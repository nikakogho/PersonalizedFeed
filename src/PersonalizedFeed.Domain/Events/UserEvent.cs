namespace PersonalizedFeed.Domain.Events;

public sealed record UserEvent(
    string TenantId,
    string UserHash,
    UserEventType Type,
    string VideoId,
    DateTimeOffset Timestamp,
    int? WatchTimeMs,
    string? FeedRequestId,
    int? RankPosition);
