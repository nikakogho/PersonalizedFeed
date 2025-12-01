namespace PersonalizedFeed.Domain.Ranking.Models;

public sealed record RankingModelDefinition(
    string ModelType,
    string ModelVersion,
    string PayloadJson);
