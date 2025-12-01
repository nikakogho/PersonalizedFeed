using Moq;
using PersonalizedFeed.Domain.Events;
using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Repositories;
using PersonalizedFeed.Domain.Services;
using Shouldly;

namespace PersonalizedFeed.Domain.Tests;

public class UserEventIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_WhenNoExistingSignals_CreatesNewAggregateFromEvents()
    {
        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        var videoRepo = new Mock<IVideoRepository>();

        userSignalsRepo
            .Setup(x => x.GetByTenantAndUserHashAsync(
                "tenant_1",
                "user_hash_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSignals?)null);

        videoRepo
            .Setup(x => x.GetByIdsAsync(
                "tenant_1",
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Video>
            {
                new()
                {
                    TenantId = "tenant_1",
                    VideoId = "vid_fitness",
                    PlaybackUrl = "",
                    ThumbnailUrl = null,
                    Title = "Fitness",
                    MainTag = "fitness",
                    Tags = new List<string> { "fitness" },
                    DurationSeconds = 30,
                    MaturityRating = "PG",
                    EditorialBoost = 0,
                    GlobalPopularityScore = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                },
                new()
                {
                    TenantId = "tenant_1",
                    VideoId = "vid_cooking",
                    PlaybackUrl = "",
                    ThumbnailUrl = null,
                    Title = "Cooking",
                    MainTag = "cooking",
                    Tags = new List<string> { "cooking" },
                    DurationSeconds = 30,
                    MaturityRating = "PG",
                    EditorialBoost = 0,
                    GlobalPopularityScore = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                }
            });

        UserSignals? saved = null;

        userSignalsRepo
            .Setup(x => x.SaveAsync(
                It.IsAny<UserSignals>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserSignals, CancellationToken>((signals, _) => saved = signals)
            .Returns(Task.CompletedTask);

        var service = new UserEventIngestionService(
            userSignalsRepo.Object,
            videoRepo.Object);

        var t0 = DateTimeOffset.UtcNow;

        var events = new List<UserEvent>
        {
            new(
                TenantId: "tenant_1",
                UserHash: "user_hash_123",
                Type: UserEventType.VideoView,
                VideoId: "vid_fitness",
                Timestamp: t0,
                WatchTimeMs: 10_000,
                FeedRequestId: "req1",
                RankPosition: 0),

            new(
                TenantId: "tenant_1",
                UserHash: "user_hash_123",
                Type: UserEventType.VideoView,
                VideoId: "vid_cooking",
                Timestamp: t0.AddSeconds(5),
                WatchTimeMs: 5_000,
                FeedRequestId: "req1",
                RankPosition: 1),

            new(
                TenantId: "tenant_1",
                UserHash: "user_hash_123",
                Type: UserEventType.Skip,
                VideoId: "vid_cooking",
                Timestamp: t0.AddSeconds(10),
                WatchTimeMs: null,
                FeedRequestId: "req1",
                RankPosition: 1)
        };

        await service.IngestAsync("tenant_1", "user_hash_123", events);

        videoRepo.Verify(x => x.GetByIdsAsync(
                "tenant_1",
                It.Is<IReadOnlyCollection<string>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains("vid_fitness") &&
                    ids.Contains("vid_cooking")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        userSignalsRepo.Verify(x => x.SaveAsync(
                It.IsAny<UserSignals>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        saved.ShouldNotBeNull();
        saved!.TenantId.ShouldBe("tenant_1");
        saved.UserHash.ShouldBe("user_hash_123");

        saved.CategoryStats.Keys.ShouldContain("fitness");
        saved.CategoryStats.Keys.ShouldContain("cooking");

        var fitness = saved.CategoryStats["fitness"];
        fitness.Views.ShouldBe(1);
        fitness.WatchTimeMs.ShouldBe(10_000);
        fitness.Skips.ShouldBe(0);

        var cooking = saved.CategoryStats["cooking"];
        cooking.Views.ShouldBe(1);
        cooking.WatchTimeMs.ShouldBe(5_000);
        cooking.Skips.ShouldBe(1);

        saved.TotalViewsLast7d.ShouldBe(2);
        saved.TotalWatchTimeLast7dMs.ShouldBe(15_000);
        saved.SkipRateLast7d.ShouldBe(0.5d);

        saved.LastActiveAt.ShouldBe(t0.AddSeconds(10));
        saved.UpdatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task IngestAsync_WhenExistingSignals_MergesEventsIntoExistingAggregate()
    {
        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        var videoRepo = new Mock<IVideoRepository>();

        var existingCategoryStats = new Dictionary<string, CategoryStats>
        {
            { "fitness", new CategoryStats(Views: 1, WatchTimeMs: 5_000, Skips: 0) }
        };

        var existingSignals = new UserSignals
        {
            TenantId = "tenant_1",
            UserHash = "user_hash_123",
            CategoryStats = existingCategoryStats,
            TotalViewsLast7d = 1,
            TotalWatchTimeLast7dMs = 5_000,
            SkipRateLast7d = 0.0,
            LastActiveAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        userSignalsRepo
            .Setup(x => x.GetByTenantAndUserHashAsync(
                "tenant_1",
                "user_hash_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSignals);

        videoRepo
            .Setup(x => x.GetByIdsAsync(
                "tenant_1",
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Video>
            {
                new()
                {
                    TenantId = "tenant_1",
                    VideoId = "vid_fitness",
                    PlaybackUrl = "",
                    ThumbnailUrl = null,
                    Title = "Fitness",
                    MainTag = "fitness",
                    Tags = new List<string> { "fitness" },
                    DurationSeconds = 30,
                    MaturityRating = "PG",
                    EditorialBoost = 0,
                    GlobalPopularityScore = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                }
            });

        UserSignals? saved = null;

        userSignalsRepo
            .Setup(x => x.SaveAsync(
                It.IsAny<UserSignals>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserSignals, CancellationToken>((signals, _) => saved = signals)
            .Returns(Task.CompletedTask);

        var service = new UserEventIngestionService(
            userSignalsRepo.Object,
            videoRepo.Object);

        var t0 = DateTimeOffset.UtcNow;

        var events = new List<UserEvent>
        {
            new(
                TenantId: "tenant_1",
                UserHash: "user_hash_123",
                Type: UserEventType.VideoView,
                VideoId: "vid_fitness",
                Timestamp: t0,
                WatchTimeMs: 7_000,
                FeedRequestId: "req2",
                RankPosition: 0),

            new(
                TenantId: "tenant_1",
                UserHash: "user_hash_123",
                Type: UserEventType.Skip,
                VideoId: "vid_fitness",
                Timestamp: t0.AddSeconds(3),
                WatchTimeMs: null,
                FeedRequestId: "req2",
                RankPosition: 0)
        };

        await service.IngestAsync("tenant_1", "user_hash_123", events);

        userSignalsRepo.Verify(x => x.SaveAsync(
                It.IsAny<UserSignals>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        saved.ShouldNotBeNull();
        saved!.CategoryStats.Keys.ShouldContain("fitness");

        var fitness = saved.CategoryStats["fitness"];

        fitness.Views.ShouldBe(2);
        fitness.WatchTimeMs.ShouldBe(12_000);
        fitness.Skips.ShouldBe(1);

        saved.TotalViewsLast7d.ShouldBe(2);
        saved.TotalWatchTimeLast7dMs.ShouldBe(12_000);
        saved.SkipRateLast7d.ShouldBe(0.5d);

        saved.LastActiveAt.ShouldBe(t0.AddSeconds(3));
    }

    [Fact]
    public async Task IngestAsync_WhenNoEvents_DoesNothing()
    {
        var userSignalsRepo = new Mock<IUserSignalsRepository>();
        var videoRepo = new Mock<IVideoRepository>();

        var service = new UserEventIngestionService(
            userSignalsRepo.Object,
            videoRepo.Object);

        var events = Array.Empty<UserEvent>();

        await service.IngestAsync("tenant_1", "user_hash_123", events);

        userSignalsRepo.Verify(x => x.GetByTenantAndUserHashAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        userSignalsRepo.Verify(x => x.SaveAsync(
                It.IsAny<UserSignals>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        videoRepo.Verify(x => x.GetByIdsAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
