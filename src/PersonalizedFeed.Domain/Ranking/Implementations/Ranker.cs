using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Ranking;

public sealed class Ranker : IRanker
{
    private readonly IFeatureExtractor _featureExtractor;
    private readonly IRankingModel _rankingModel;
    private readonly IFeedDiversifier _diversifier;

    public Ranker(
        IFeatureExtractor featureExtractor,
        IRankingModel rankingModel,
        IFeedDiversifier diversifier)
    {
        _featureExtractor = featureExtractor;
        _rankingModel = rankingModel;
        _diversifier = diversifier;
    }

    public IReadOnlyList<RankedVideo> Rank(
        TenantConfig tenant,
        UserSignals? user,
        IReadOnlyList<Video> candidates,
        RankingModelDefinition modelDefinition,
        int limit)
    {
        var now = DateTimeOffset.UtcNow;

        var scored = candidates
            .Select(video =>
            {
                var features = _featureExtractor.ToFeatures(tenant, user, video, now);
                var score = _rankingModel.Score(features, modelDefinition);
                return new RankedVideo(video, score);
            })
            .OrderByDescending(rv => rv.Score)
            .ToList();

        return _diversifier.Diversify(scored, limit);
    }
}
