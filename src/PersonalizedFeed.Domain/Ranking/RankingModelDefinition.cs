namespace PersonalizedFeed.Domain.Ranking;

public sealed record RankingModelDefinition(
    string ModelType,
    string ModelVersion,
    string PayloadJson);
