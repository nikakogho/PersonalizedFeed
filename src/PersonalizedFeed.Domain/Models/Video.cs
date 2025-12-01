namespace PersonalizedFeed.Domain.Models;

public sealed record Video
{
    public required string TenantId { get; init; }
    public required string VideoId { get; init; }

    public required string PlaybackUrl { get; init; }
    public string? ThumbnailUrl { get; init; }

    public required string Title { get; init; }
    public required string MainTag { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public int DurationSeconds { get; init; }

    public string MaturityRating { get; init; } = "PG";
    public double EditorialBoost { get; init; }
    public double GlobalPopularityScore { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsActive { get; init; } = true;
}
