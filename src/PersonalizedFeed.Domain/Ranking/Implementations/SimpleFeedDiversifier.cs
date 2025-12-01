using PersonalizedFeed.Domain.Helpers;

namespace PersonalizedFeed.Domain.Ranking;

public sealed class SimpleFeedDiversifier : IFeedDiversifier
{
    private readonly double _maxTitleSimilarity;
    private readonly int _maxSameMainTagInRow;

    public SimpleFeedDiversifier(
        double maxTitleSimilarity = 0.8,
        int maxSameMainTagInRow = 3)
    {
        _maxTitleSimilarity = maxTitleSimilarity;
        _maxSameMainTagInRow = maxSameMainTagInRow;
    }

    public IReadOnlyList<RankedVideo> Diversify(
        IReadOnlyList<RankedVideo> scored,
        int limit)
    {
        var result = new List<RankedVideo>(limit);
        var usedTitles = new List<string>();

        string? lastMainTag = null;
        var sameTagStreak = 0;

        foreach (var item in scored)
        {
            if (result.Count >= limit)
                break;

            // 1) Skip near-duplicate titles
            if (usedTitles.Any(t => TitleSimilarity(t, item.Video.Title) >= _maxTitleSimilarity))
                continue;

            // 2) Cap long streaks of identical category
            var mainTag = item.Video.MainTag;
            if (lastMainTag == mainTag)
            {
                sameTagStreak++;
                if (sameTagStreak > _maxSameMainTagInRow)
                    continue;
            }
            else
            {
                lastMainTag = mainTag;
                sameTagStreak = 1;
            }

            result.Add(item);
            usedTitles.Add(item.Video.Title);
        }

        return result;
    }

    private static double TitleSimilarity(string a, string b)
    {
        return TextSimilarityHelper.NormalizedEditSimilarity(a, b); // 0 for completely different, 1 for identical
    }
}
