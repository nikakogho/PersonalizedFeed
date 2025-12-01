using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Ranking;

public sealed class SimpleFeatureExtractor : IFeatureExtractor
{
    public RankingFeatures ToFeatures(
        TenantConfig tenant,
        UserSignals? user,
        Video video,
        DateTimeOffset now)
    {
        var recencyHours = Math.Max(0, (now - video.CreatedAt).TotalHours);

        double categoryAffinity = 0.0; // share of this category in user’s total views (0..1)
        double userWatchTimeLast7dSeconds = 0.0;
        double userSkipRateLast7d = 0.0;
        string userHash = string.Empty;

        if (user is not null)
        {
            userHash = user.UserHash;

            if (user.TotalViewsLast7d > 0 &&
                user.CategoryStats.TryGetValue(video.MainTag, out var statsForCategory))
            {
                categoryAffinity = (double)statsForCategory.Views / user.TotalViewsLast7d;
            }

            userWatchTimeLast7dSeconds = user.TotalWatchTimeLast7dMs / 1000.0; // convert ms -> seconds for scale
            userSkipRateLast7d = user.SkipRateLast7d;
        }

        bool isMatureContent = IsMature(video.MaturityRating);

        return new RankingFeatures(
            TenantId: tenant.TenantId,
            UserHash: userHash,
            VideoId: video.VideoId,
            MainTag: video.MainTag,
            CategoryAffinity: categoryAffinity,
            RecencyHours: recencyHours,
            GlobalPopularityScore: video.GlobalPopularityScore,
            EditorialBoost: video.EditorialBoost,
            UserWatchTimeLast7d: userWatchTimeLast7dSeconds,
            UserSkipRateLast7d: userSkipRateLast7d,
            IsMatureContent: isMatureContent);
    }

    private static bool IsMature(string? rating)
    {
        if (string.IsNullOrWhiteSpace(rating))
            return false;

        rating = rating.Trim().ToUpperInvariant();

        // Super simple heuristic, we can refine later
        return rating is "R" or "NC17" or "18+" or "M";
    }
}
