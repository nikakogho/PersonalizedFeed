namespace PersonalizedFeed.Domain.Models;

public sealed record UserSignals
{
    public required string TenantId { get; init; }
    public required string UserHash { get; init; }

    public IReadOnlyDictionary<string, CategoryStats> CategoryStats { get; init; }
        = new Dictionary<string, CategoryStats>();

    public int TotalViewsLast7d { get; init; }
    public long TotalWatchTimeLast7dMs { get; init; }
    public double SkipRateLast7d { get; init; }

    public DateTimeOffset LastActiveAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
