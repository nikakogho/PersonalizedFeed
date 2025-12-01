namespace PersonalizedFeed.Api.Contracts;

public sealed record FeedItemResponse(
    string VideoId,
    string PlaybackUrl,
    string? ThumbnailUrl,
    string Title,
    string MainTag,
    IReadOnlyList<string> Tags,
    int DurationSeconds,
    string MaturityRating,
    double Score,
    int Rank);

public sealed record FeedResponse(
    string Mode,
    IReadOnlyList<FeedItemResponse> Items,
    string? NextCursor);
