using PersonalizedFeed.Domain.Ranking.Models;

namespace PersonalizedFeed.Domain.Ranking;

public interface IRankingModel
{
    double Score(RankingFeatures features, RankingModelDefinition modelDefinition);
}
