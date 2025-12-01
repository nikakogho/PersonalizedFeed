namespace PersonalizedFeed.Domain.Services.Models;

public sealed record FeedResult(
    FeedMode Mode,
    IReadOnlyList<FeedItem> Items,
    string? NextCursor);
