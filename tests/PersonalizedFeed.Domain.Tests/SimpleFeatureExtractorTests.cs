using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking;

namespace PersonalizedFeed.Domain.Tests;

public class SimpleFeatureExtractorTests
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
            RankingModelVersion = "1.0.0",
            RankingModelPayloadJson = null,
            FeatureFlagsJson = null
        };

    private static Video CreateVideo(string videoId, string mainTag, string maturity = "PG")
        => new()
        {
            TenantId = "tenant_1",
            VideoId = videoId,
            PlaybackUrl = $"https://cdn.example.com/v/{videoId}.m3u8",
            ThumbnailUrl = null,
            Title = $"Title {videoId}",
            MainTag = mainTag,
            Tags = new List<string> { mainTag },
            DurationSeconds = 30,
            MaturityRating = maturity,
            EditorialBoost = 1.0,
            GlobalPopularityScore = 10.0,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            IsActive = true
        };

    [Fact]
    public void ToFeatures_ColdStart_UsesDefaults()
    {
        // Arrange
        var tenant = CreateTenant();
        Video video = CreateVideo("vid_1", "fitness");

        UserSignals? user = null;
        var now = DateTimeOffset.UtcNow;

        var extractor = new SimpleFeatureExtractor();

        // Act
        var features = extractor.ToFeatures(tenant, user, video, now);

        // Assert
        Assert.Equal("tenant_1", features.TenantId);
        Assert.Equal(string.Empty, features.UserHash);
        Assert.Equal("vid_1", features.VideoId);
        Assert.Equal("fitness", features.MainTag);

        Assert.Equal(0.0, features.CategoryAffinity);
        Assert.Equal(0.0, features.UserWatchTimeLast7d);
        Assert.Equal(0.0, features.UserSkipRateLast7d);

        Assert.True(features.RecencyHours >= 1 && features.RecencyHours <= 3);
        Assert.Equal(10.0, features.GlobalPopularityScore);
        Assert.Equal(1.0, features.EditorialBoost);
        Assert.False(features.IsMatureContent);
    }

    [Fact]
    public void ToFeatures_WarmUser_ComputesCategoryAffinityAndSignals()
    {
        // Arrange
        var tenant = CreateTenant();
        var video = CreateVideo("vid_2", "fitness");

        var categoryStats = new Dictionary<string, CategoryStats>
        {
            // User watched fitness 8 times out of 10 total views
            { "fitness", new CategoryStats(Views: 8, WatchTimeMs: 120_000, Skips: 1) },
            { "cooking", new CategoryStats(Views: 2, WatchTimeMs: 10_000, Skips: 0) }
        };

        var user = new UserSignals
        {
            TenantId = "tenant_1",
            UserHash = "user_hash_123",
            CategoryStats = categoryStats,
            TotalViewsLast7d = 10,
            TotalWatchTimeLast7dMs = 130_000,
            SkipRateLast7d = 0.1,
            LastActiveAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var now = DateTimeOffset.UtcNow;
        var extractor = new SimpleFeatureExtractor();

        // Act
        var features = extractor.ToFeatures(tenant, user, video, now);

        // Assert
        Assert.Equal("tenant_1", features.TenantId);
        Assert.Equal("user_hash_123", features.UserHash);

        // CategoryAffinity = fitnessViews / totalViews = 8 / 10 = 0.8
        Assert.InRange(features.CategoryAffinity, 0.79, 0.81);

        // Watch time seconds ≈ 130_000 / 1000 = 130
        Assert.InRange(features.UserWatchTimeLast7d, 129.0, 131.0);

        Assert.Equal(0.1, features.UserSkipRateLast7d, precision: 3);
    }

    [Fact]
    public void ToFeatures_MarksMatureContent()
    {
        // Arrange
        var tenant = CreateTenant();
        var matureVideo = CreateVideo("vid_m", "thriller", maturity: "R");

        var user = new UserSignals
        {
            TenantId = "tenant_1",
            UserHash = "user_hash_123",
            CategoryStats = new Dictionary<string, CategoryStats>(),
            TotalViewsLast7d = 0,
            TotalWatchTimeLast7dMs = 0,
            SkipRateLast7d = 0,
            LastActiveAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var now = DateTimeOffset.UtcNow;
        var extractor = new SimpleFeatureExtractor();

        // Act
        var features = extractor.ToFeatures(tenant, user, matureVideo, now);

        // Assert
        Assert.True(features.IsMatureContent);
    }
}
