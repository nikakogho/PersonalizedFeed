using PersonalizedFeed.Domain.Ranking;
using PersonalizedFeed.Domain.Ranking.Models;
using Shouldly;
using System.Text.Json;

namespace PersonalizedFeed.Domain.Tests;

public class LinearRankingModelTests
{
    [Fact]
    public void Score_Computes_Expected_Value()
    {
        var features = new RankingFeatures(
            TenantId: "tenant",
            UserHash: "user",
            VideoId: "vid",
            MainTag: "fitness",
            CategoryAffinity: 2.0,
            RecencyHours: 3.0,
            GlobalPopularityScore: 4.0,
            EditorialBoost: 5.0,
            UserWatchTimeLast7d: 6.0,
            UserSkipRateLast7d: 0.5,
            IsMatureContent: false);

        var weights = new LinearWeights(
            CategoryAffinity: 2.0,
            RecencyHours: -1.0,
            GlobalPopularityScore: 0.5,
            EditorialBoost: 1.0,
            UserWatchTimeLast7d: 0.01,
            UserSkipRateLast7d: -3.0,
            IsMatureContent: -100.0);

        var config = new LinearModelConfig(weights, Bias: 0.5);

        var definition = new RankingModelDefinition(
            ModelType: "linear",
            ModelVersion: "1.0.0",
            PayloadJson: JsonSerializer.Serialize(config));

        var model = new LinearRankingModel();

        var score = model.Score(features, definition);

        const double expected = 7.06;
        score.ShouldBe(expected, tolerance: 0.01);
    }
}
