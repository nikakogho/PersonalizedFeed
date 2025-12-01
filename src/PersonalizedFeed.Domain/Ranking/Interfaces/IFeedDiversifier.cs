using PersonalizedFeed.Domain.Ranking.Models;

namespace PersonalizedFeed.Domain.Ranking;

public interface IFeedDiversifier
{
    IReadOnlyList<RankedVideo> Diversify(
        IReadOnlyList<RankedVideo> scored,
        int limit);
}
