using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking;
using Shouldly;
using System.Text.Json;

namespace PersonalizedFeed.Domain.Tests;

public class RankerIntegrationTests
{
    private static TenantConfig CreateTenant() =>
        new()
        {
            TenantId = "tenant_1",
            ApiKey = "secret-key",
            UsePersonalization = true,
            DefaultLimit = 20,
            MaturityPolicy = "PG13",
            RankingModelType = "linear",
            RankingModelVersion = "test-weights",
            RankingModelPayloadJson = null,
            FeatureFlagsJson = null
        };

    private static UserSignals CreateUserSignals()
    {
        var categoryStatsUserPrefersFitness = new Dictionary<string, CategoryStats>
        {
            { "fitness", new CategoryStats(Views: 8, WatchTimeMs: 120_000, Skips: 1) },
            { "cooking", new CategoryStats(Views: 2, WatchTimeMs: 10_000, Skips: 0) }
        };

        return new UserSignals
        {
            TenantId = "tenant_1",
            UserHash = "user_hash_123",
            CategoryStats = categoryStatsUserPrefersFitness,
            TotalViewsLast7d = 10,
            TotalWatchTimeLast7dMs = 130_000,
            SkipRateLast7d = 0.1,
            LastActiveAt = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero)
        };
    }

    private static Video CreateVideo(
        string videoId,
        string mainTag,
        double popularity)
        => new()
        {
            TenantId = "tenant_1",
            VideoId = videoId,
            PlaybackUrl = $"https://cdn.example.com/v/{videoId}.m3u8",
            ThumbnailUrl = null,
            Title = $"Sample {mainTag} video",
            MainTag = mainTag,
            Tags = new List<string> { mainTag },
            DurationSeconds = 30,
            MaturityRating = "PG",
            EditorialBoost = 0.0,
            GlobalPopularityScore = popularity,
            CreatedAt = new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 1, 1, 11, 30, 0, TimeSpan.Zero),
            IsActive = true
        };

    [Fact]
    public void Ranker_PrefersUserAffinityOverPopularity()
    {
        // Arrange
        var tenant = CreateTenant();
        var user = CreateUserSignals();

        var now = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var fitnessVideo = CreateVideo("vid_fitness", "fitness", popularity: 5.0);
        var cookingVideo = CreateVideo("vid_cooking", "cooking", popularity: 20.0);

        var candidates = new[] { fitnessVideo, cookingVideo };

        var weights = new LinearWeights(
            CategoryAffinity: 15.0,
            RecencyHours: -0.1,
            GlobalPopularityScore: 0.5,
            EditorialBoost: 0.0,
            UserWatchTimeLast7d: 0.0,
            UserSkipRateLast7d: 0.0,
            IsMatureContent: -100.0);

        var config = new LinearModelConfig(weights, Bias: 0.0);

        var modelDefinition = new RankingModelDefinition(
            ModelType: "linear",
            ModelVersion: "test-weights",
            PayloadJson: JsonSerializer.Serialize(config));

        var featureExtractor = new SimpleFeatureExtractor();
        var rankingModel = new LinearRankingModel();
        var diversifier = new SimpleFeedDiversifier(
            maxTitleSimilarity: 0.99,
            maxSameMainTagInRow: 5);

        var ranker = new Ranker(featureExtractor, rankingModel, diversifier);

        // Act
        var ranked = ranker.Rank(
            tenant,
            user,
            candidates,
            modelDefinition,
            limit: 2);

        // Assert
        ranked.Count.ShouldBe(2);
        ranked[0].Video.VideoId.ShouldBe("vid_fitness");
        ranked[1].Video.VideoId.ShouldBe("vid_cooking");
    }
}
