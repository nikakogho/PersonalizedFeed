using Moq;
using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking;
using PersonalizedFeed.Domain.Repositories;
using PersonalizedFeed.Domain.Services;
using PersonalizedFeed.Domain.Services.Models;
using Shouldly;
using System.Text.Json;

namespace PersonalizedFeed.Domain.Tests;

public class FeedServiceTests
{
    [Fact]
    public async Task PersonalizedFeed_PrefersUserAffinityOverPopularity()
    {
        // Arrange
        var tenantConfig = CreateTenantConfigWithStrongCategoryWeight();
        var userSignals = CreateUserSignalsWithFitnessPreference();
        var candidates = CreateCandidateVideos();

        var tenantRepo = new Mock<ITenantConfigRepository>();
        tenantRepo
            .Setup(x => x.GetByTenantIdAndApiKeyAsync(
                "tenant_1",
                "secret-api-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantConfig);

        var systemConfigRepo = new Mock<ISystemConfigRepository>();
        systemConfigRepo
            .Setup(x => x.IsPersonalizationGloballyEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        userSignalsRepo
            .Setup(x => x.GetByTenantAndUserHashAsync(
                "tenant_1",
                "user_hash_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(userSignals);

        var videoRepo = new Mock<IVideoRepository>();
        videoRepo
            .Setup(x => x.GetCandidateVideosAsync(
                "tenant_1",
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        var featureExtractor = new SimpleFeatureExtractor();
        var rankingModel = new LinearRankingModel();
        var diversifier = new SimpleFeedDiversifier(
            maxTitleSimilarity: 0.99,
            maxSameMainTagInRow: 5);
        var ranker = new Ranker(featureExtractor, rankingModel, diversifier);

        var service = new FeedService(
            tenantRepo.Object,
            systemConfigRepo.Object,
            userSignalsRepo.Object,
            videoRepo.Object,
            ranker);

        var request = new FeedRequest(
            TenantId: "tenant_1",
            ApiKey: "secret-api-key",
            UserHash: "user_hash_123",
            Limit: 2);

        // Act
        var result = await service.GetFeedAsync(request);

        // Assert
        result.Mode.ShouldBe(FeedMode.Personalized);
        result.Items.Count.ShouldBe(2);
        result.Items[0].Video.VideoId.ShouldBe("vid_fitness");
        result.Items[1].Video.VideoId.ShouldBe("vid_cooking");
    }

    [Fact]
    public async Task FallbackFeed_WhenPersonalizationDisabledGlobally()
    {
        // Arrange
        var tenantConfig = CreateTenantConfigWithStrongCategoryWeight();
        var candidates = CreateCandidateVideos();

        var tenantRepo = new Mock<ITenantConfigRepository>();
        tenantRepo
            .Setup(x => x.GetByTenantIdAndApiKeyAsync(
                "tenant_1",
                "secret-api-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantConfig);

        var systemConfigRepo = new Mock<ISystemConfigRepository>();
        systemConfigRepo
            .Setup(x => x.IsPersonalizationGloballyEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // global switch OFF

        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        userSignalsRepo
            .Setup(x => x.GetByTenantAndUserHashAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSignals?)null);

        var videoRepo = new Mock<IVideoRepository>();
        videoRepo
            .Setup(x => x.GetCandidateVideosAsync(
                "tenant_1",
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        var featureExtractor = new SimpleFeatureExtractor();
        var rankingModel = new LinearRankingModel();
        var diversifier = new SimpleFeedDiversifier(
            maxTitleSimilarity: 0.99,
            maxSameMainTagInRow: 5);
        var ranker = new Ranker(featureExtractor, rankingModel, diversifier);

        var service = new FeedService(
            tenantRepo.Object,
            systemConfigRepo.Object,
            userSignalsRepo.Object,
            videoRepo.Object,
            ranker);

        var request = new FeedRequest(
            TenantId: "tenant_1",
            ApiKey: "secret-api-key",
            UserHash: "unknown_user",
            Limit: 2);

        // Act
        var result = await service.GetFeedAsync(request);

        // Assert
        result.Mode.ShouldBe(FeedMode.Fallback);
        result.Items.Count.ShouldBe(2);
    }

    private static TenantConfig CreateTenantConfigWithStrongCategoryWeight()
    {
        var weights = new LinearWeights(
            CategoryAffinity: 15.0,
            RecencyHours: -0.1,
            GlobalPopularityScore: 0.5,
            EditorialBoost: 0.0,
            UserWatchTimeLast7d: 0.0,
            UserSkipRateLast7d: 0.0,
            IsMatureContent: -100.0);

        var config = new LinearModelConfig(weights, Bias: 0.0);
        var payloadJson = JsonSerializer.Serialize(config);

        return new TenantConfig
        {
            TenantId = "tenant_1",
            ApiKey = "secret-api-key",
            UsePersonalization = true,
            DefaultLimit = 20,
            MaturityPolicy = "PG13",
            RankingModelType = "linear",
            RankingModelVersion = "test-weights",
            RankingModelPayloadJson = payloadJson,
            FeatureFlagsJson = null
        };
    }

    private static UserSignals CreateUserSignalsWithFitnessPreference()
    {
        var categoryStats = new Dictionary<string, CategoryStats>
        {
            { "fitness", new CategoryStats(Views: 8, WatchTimeMs: 120_000, Skips: 1) },
            { "cooking", new CategoryStats(Views: 2, WatchTimeMs: 10_000, Skips: 0) }
        };

        return new UserSignals
        {
            TenantId = "tenant_1",
            UserHash = "user_hash_123",
            CategoryStats = categoryStats,
            TotalViewsLast7d = 10,
            TotalWatchTimeLast7dMs = 130_000,
            SkipRateLast7d = 0.1,
            LastActiveAt = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero)
        };
    }

    private static IReadOnlyList<Video> CreateCandidateVideos()
    {
        var now = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var fitness = new Video
        {
            TenantId = "tenant_1",
            VideoId = "vid_fitness",
            PlaybackUrl = "https://cdn.example.com/v/vid_fitness.m3u8",
            ThumbnailUrl = null,
            Title = "Fitness warmup",
            MainTag = "fitness",
            Tags = new List<string> { "fitness" },
            DurationSeconds = 30,
            MaturityRating = "PG",
            EditorialBoost = 0.0,
            GlobalPopularityScore = 5.0,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-1),
            IsActive = true
        };

        var cooking = new Video
        {
            TenantId = "tenant_1",
            VideoId = "vid_cooking",
            PlaybackUrl = "https://cdn.example.com/v/vid_cooking.m3u8",
            ThumbnailUrl = null,
            Title = "Cooking pasta",
            MainTag = "cooking",
            Tags = new List<string> { "cooking" },
            DurationSeconds = 30,
            MaturityRating = "PG",
            EditorialBoost = 0.0,
            GlobalPopularityScore = 20.0,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-1),
            IsActive = true
        };

        return new[] { fitness, cooking };
    }

    [Fact]
    public async Task FeedService_PassesTenantMaturityPolicyToVideoRepository()
    {
        // Arrange
        var tenantConfig = CreateTenantConfigWithStrongCategoryWeight();
        tenantConfig = tenantConfig with { MaturityPolicy = "PG13" };

        var userSignals = CreateUserSignalsWithFitnessPreference();
        var candidates = CreateCandidateVideos();

        var tenantRepo = new Mock<ITenantConfigRepository>();
        tenantRepo
            .Setup(x => x.GetByTenantIdAndApiKeyAsync(
                "tenant_1",
                "secret-api-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantConfig);

        var systemConfigRepo = new Mock<ISystemConfigRepository>();
        systemConfigRepo
            .Setup(x => x.IsPersonalizationGloballyEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        userSignalsRepo
            .Setup(x => x.GetByTenantAndUserHashAsync(
                "tenant_1",
                "user_hash_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(userSignals);

        var videoRepo = new Mock<IVideoRepository>();
        videoRepo
            .Setup(x => x.GetCandidateVideosAsync(
                "tenant_1",
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        var featureExtractor = new SimpleFeatureExtractor();
        var rankingModel = new LinearRankingModel();
        var diversifier = new SimpleFeedDiversifier(
            maxTitleSimilarity: 0.99,
            maxSameMainTagInRow: 5);
        var ranker = new Ranker(featureExtractor, rankingModel, diversifier);

        var service = new FeedService(
            tenantRepo.Object,
            systemConfigRepo.Object,
            userSignalsRepo.Object,
            videoRepo.Object,
            ranker);

        var request = new FeedRequest(
            TenantId: "tenant_1",
            ApiKey: "secret-api-key",
            UserHash: "user_hash_123",
            Limit: 10);

        // Act
        await service.GetFeedAsync(request);

        // Assert – ensure we pass the exact maturity policy down
        videoRepo.Verify(v => v.GetCandidateVideosAsync(
                "tenant_1",
                It.IsAny<int>(),
                "PG13",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
