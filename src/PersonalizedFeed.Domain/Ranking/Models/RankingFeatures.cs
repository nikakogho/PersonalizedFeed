namespace PersonalizedFeed.Domain.Ranking.Models;

public sealed record RankingFeatures(
    string TenantId,
    string UserHash,
    string VideoId,
    string MainTag,
    double CategoryAffinity,
    double RecencyHours,
    double GlobalPopularityScore,
    double EditorialBoost,
    double UserWatchTimeLast7d,
    double UserSkipRateLast7d,
    bool IsMatureContent);
