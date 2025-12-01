using PersonalizedFeed.Domain.Ranking.Models;
using System.Text.Json;

namespace PersonalizedFeed.Domain.Ranking;

public sealed record LinearWeights(
    double CategoryAffinity,
    double RecencyHours,
    double GlobalPopularityScore,
    double EditorialBoost,
    double UserWatchTimeLast7d,
    double UserSkipRateLast7d,
    double IsMatureContent);

public sealed record LinearModelConfig(LinearWeights Weights, double Bias);

public sealed class LinearRankingModel : IRankingModel
{
    public double Score(RankingFeatures f, RankingModelDefinition modelDefinition)
    {
        if (!string.Equals(modelDefinition.ModelType, "linear", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "ModelType must be 'linear' for LinearRankingModel.",
                nameof(modelDefinition));
        }

        var config = JsonSerializer.Deserialize<LinearModelConfig>(modelDefinition.PayloadJson)
                     ?? throw new InvalidOperationException("Failed to deserialize LinearModelConfig.");

        var w = config.Weights;

        var score =
            w.CategoryAffinity * f.CategoryAffinity +
            w.RecencyHours * f.RecencyHours +
            w.GlobalPopularityScore * f.GlobalPopularityScore +
            w.EditorialBoost * f.EditorialBoost +
            w.UserWatchTimeLast7d * f.UserWatchTimeLast7d +
            w.UserSkipRateLast7d * f.UserSkipRateLast7d +
            w.IsMatureContent * (f.IsMatureContent ? 1.0 : 0.0) +
            config.Bias;

        return score;
    }
}
