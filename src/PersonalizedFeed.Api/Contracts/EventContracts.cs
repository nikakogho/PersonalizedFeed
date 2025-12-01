namespace PersonalizedFeed.Api.Contracts;

public sealed record UserEventRequest(
    string Type,
    string VideoId,
    DateTimeOffset Timestamp,
    int? WatchTimeMs,
    string? FeedRequestId,
    int? RankPosition);

public sealed record UserEventBatchRequest(
    IReadOnlyList<UserEventRequest> Events);
