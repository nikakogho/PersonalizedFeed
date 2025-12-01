namespace PersonalizedFeed.Domain.Models;

public sealed record VideoStats
{
    public required string TenantId { get; init; }
    public required string VideoId { get; init; }

    public int ViewsLast7d { get; init; }
    public int LikesLast7d { get; init; }
    public double AvgWatchTimeLast7dMs { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
