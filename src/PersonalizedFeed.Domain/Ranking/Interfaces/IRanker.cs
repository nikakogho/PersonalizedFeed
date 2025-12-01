using PersonalizedFeed.Domain.Models;

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
