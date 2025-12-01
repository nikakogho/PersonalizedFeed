namespace PersonalizedFeed.Domain.Ranking;

public interface IRankingModel
{
    double Score(RankingFeatures features, RankingModelDefinition modelDefinition);
}
