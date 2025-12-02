namespace PersonalizedFeed.Domain.Policies;

public static class MaturityRatingPolicy
{
    public static bool IsAllowed(string videoRating, string policyRating)
    {
        var videoScore = Score(videoRating);
        var policyScore = Score(policyRating);

        // allow only ratings up to the policy level
        return videoScore <= policyScore;
    }

    private static int Score(string? rating) =>
        rating?.Trim().ToUpperInvariant() switch
        {
            "G" => 0,
            "PG" => 1,
            "PG13" or "PG-13" => 2,
            "R" => 3,
            "NC17" or "NC-17" => 4,
            _ => 4 // unknown/missing = most restrictive (fail-closed)
        };
}
