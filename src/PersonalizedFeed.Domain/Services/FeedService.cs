using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Ranking;
using PersonalizedFeed.Domain.Ranking.Models;
using PersonalizedFeed.Domain.Repositories;
using PersonalizedFeed.Domain.Services.Models;
using System.Text.Json;

namespace PersonalizedFeed.Domain.Services;

public sealed class FeedService : IFeedService
{
    private const int MaxLimit = 50;
    private const int DefaultCandidatePoolSize = 200;

    private readonly ITenantConfigRepository _tenantConfigs;
    private readonly ISystemConfigRepository _systemConfig;
    private readonly IUserSignalsRepository _userSignals;
    private readonly IVideoRepository _videos;
    private readonly IRanker _ranker;

    public FeedService(
        ITenantConfigRepository tenantConfigs,
        ISystemConfigRepository systemConfig,
        IUserSignalsRepository userSignals,
        IVideoRepository videos,
        IRanker ranker)
    {
        _tenantConfigs = tenantConfigs;
        _systemConfig = systemConfig;
        _userSignals = userSignals;
        _videos = videos;
        _ranker = ranker;
    }

    public async Task<FeedResult> GetFeedAsync(
        FeedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.UserHash))
            throw new ArgumentException("UserHash is required.", nameof(request));

        var tenant =
            await _tenantConfigs.GetByTenantIdAndApiKeyAsync(
                request.TenantId,
                request.ApiKey,
                cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid tenant or API key.");

        var globallyEnabled =
            await _systemConfig.IsPersonalizationGloballyEnabledAsync(cancellationToken);

        var personalizationEnabled = globallyEnabled && tenant.UsePersonalization;

        var requestedLimit = request.Limit ?? tenant.DefaultLimit;
        var effectiveLimit = Math.Clamp(requestedLimit, 1, MaxLimit);

        UserSignals? userSignals = null;
        if (personalizationEnabled)
        {
            userSignals = await _userSignals.GetByTenantAndUserHashAsync(
                request.TenantId,
                request.UserHash,
                cancellationToken);
        }

        var candidates = await _videos.GetCandidateVideosAsync(
            request.TenantId,
            DefaultCandidatePoolSize,
            cancellationToken);

        var rankingDefinition = CreateModelDefinition(tenant);

        var rankedVideos = _ranker.Rank(
            tenant,
            userSignals,
            candidates,
            rankingDefinition,
            effectiveLimit);

        var items = rankedVideos
            .Select((rv, index) => new FeedItem(rv.Video, rv.Score, index))
            .ToList();

        var mode = personalizationEnabled && userSignals is not null
            ? FeedMode.Personalized
            : FeedMode.Fallback;

        return new FeedResult(
            Mode: mode,
            Items: items,
            NextCursor: null);
    }

    private static RankingModelDefinition CreateModelDefinition(TenantConfig tenant)
    {
        if (!string.Equals(tenant.RankingModelType, "linear", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Only 'linear' ranking model is supported in this version. Got '{tenant.RankingModelType}'.");
        }

        var payloadJson = tenant.RankingModelPayloadJson;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            return new RankingModelDefinition(
                tenant.RankingModelType,
                tenant.RankingModelVersion,
                payloadJson);
        }

        var defaultWeights = new LinearWeights(
            CategoryAffinity: 5.0,
            RecencyHours: -0.05,
            GlobalPopularityScore: 1.0,
            EditorialBoost: 1.0,
            UserWatchTimeLast7d: 0.001,
            UserSkipRateLast7d: -1.0,
            IsMatureContent: -100.0);

        var defaultConfig = new LinearModelConfig(defaultWeights, Bias: 0.0);

        var json = JsonSerializer.Serialize(defaultConfig);

        return new RankingModelDefinition(
            ModelType: "linear",
            ModelVersion: "default-1",
            PayloadJson: json);
    }
}
