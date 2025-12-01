using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking.Models;

namespace PersonalizedFeed.Domain.Ranking;

public interface IFeatureExtractor
{
    RankingFeatures ToFeatures(
        TenantConfig tenant,
        UserSignals? user,
        Video video,
        DateTimeOffset now);
}
