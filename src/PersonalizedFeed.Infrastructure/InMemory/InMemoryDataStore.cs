using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking;
using System.Collections.Concurrent;

namespace PersonalizedFeed.Infrastructure.InMemory;

public sealed class InMemoryDataStore
{
    public ConcurrentDictionary<string, TenantConfig> Tenants { get; } = new();
    public ConcurrentDictionary<(string TenantId, string UserHash), UserSignals> UserSignals { get; } = new();
    public ConcurrentDictionary<(string TenantId, string VideoId), Video> Videos { get; } = new();

    public bool PersonalizationEnabledGlobally { get; set; } = true;

    public InMemoryDataStore()
    {
        Seed();
    }

    void Seed()
    {
        var tenantId = "tenant_1";
        var apiKey = "secret-api-key";

        var weights = new LinearWeights(
            CategoryAffinity: 15.0,
            RecencyHours: -0.1,
            GlobalPopularityScore: 0.5,
            EditorialBoost: 0.0,
            UserWatchTimeLast7d: 0.0,
            UserSkipRateLast7d: 0.0,
            IsMatureContent: -100.0);

        var config = new LinearModelConfig(weights, Bias: 0.0);

        var tenantConfig = new TenantConfig
        {
            TenantId = tenantId,
            ApiKey = apiKey,
            UsePersonalization = true,
            DefaultLimit = 20,
            MaturityPolicy = "PG13",
            RankingModelType = "linear",
            RankingModelVersion = "seed-weights",
            RankingModelPayloadJson = System.Text.Json.JsonSerializer.Serialize(config),
            FeatureFlagsJson = null
        };

        Tenants[tenantId] = tenantConfig;

        var now = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var fitnessVideo = new Video
        {
            TenantId = tenantId,
            VideoId = "vid_fitness_1",
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

        var cookingVideo = new Video
        {
            TenantId = tenantId,
            VideoId = "vid_cooking_1",
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

        Videos[(tenantId, fitnessVideo.VideoId)] = fitnessVideo;
        Videos[(tenantId, cookingVideo.VideoId)] = cookingVideo;

        var categoryStats = new Dictionary<string, CategoryStats>
        {
            { "fitness", new CategoryStats(Views: 8, WatchTimeMs: 120_000, Skips: 1) },
            { "cooking", new CategoryStats(Views: 2, WatchTimeMs: 10_000, Skips: 0) }
        };

        var userSignals = new UserSignals
        {
            TenantId = tenantId,
            UserHash = "user_hash_123",
            CategoryStats = categoryStats,
            TotalViewsLast7d = 10,
            TotalWatchTimeLast7dMs = 130_000,
            SkipRateLast7d = 0.1,
            LastActiveAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-1)
        };

        UserSignals[(tenantId, userSignals.UserHash)] = userSignals;
    }
}
