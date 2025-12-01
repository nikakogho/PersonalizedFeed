using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking.Models;

namespace PersonalizedFeed.Domain.Ranking;

public interface IRanker
{
    IReadOnlyList<RankedVideo> Rank(
        TenantConfig tenant,
        UserSignals? user,
        IReadOnlyList<Video> candidates,
        RankingModelDefinition modelDefinition,
        int limit);
}
