namespace PersonalizedFeed.Domain.Models;

public sealed record CategoryStats(
    int Views,
    long WatchTimeMs,
    int Skips);
