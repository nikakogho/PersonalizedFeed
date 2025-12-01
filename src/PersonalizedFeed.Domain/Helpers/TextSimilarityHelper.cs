namespace PersonalizedFeed.Domain.Helpers;

public static class TextSimilarityHelper
{
    /// <summary>
    /// Computes normalized similarity (0..1) between two titles
    /// using normalized Levenshtein (minimum edit distance).
    /// 
    /// 1.0 = identical
    /// 0.0 = completely different
    /// </summary>
    public static double NormalizedEditSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0;

        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();

        if (a == b)
            return 1.0;

        int maxLength = Math.Max(a.Length, b.Length);
        if (maxLength == 0)
            return 0;

        int editDistance = MinimumEditDistance(a, b);

        // Normalize: similarity = 1 - (distance / maxLength)
        return 1.0 - (double)editDistance / maxLength;
    }


    /// <summary>
    /// Computes the minimum number of edit operations required
    /// to transform string 'source' into 'target'.
    ///
    /// Allowed operations:
    /// - Insert a character
    /// - Delete a character
    /// - Substitute one character for another
    ///
    /// Uses dynamic programming, O(n*m) time and space.
    /// </summary>
    private static int MinimumEditDistance(string source, string target)
    {
        int sourceLength = source.Length;
        int targetLength = target.Length;

        // DP matrix: each cell [i,j] = min edits to convert
        // first i chars of source → first j chars of target
        int[,] dp = new int[sourceLength + 1, targetLength + 1];

        // Base cases: converting empty prefix → prefix of other string
        for (int i = 0; i <= sourceLength; i++)
            dp[i, 0] = i; // i deletions

        for (int j = 0; j <= targetLength; j++)
            dp[0, j] = j; // j insertions

        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                bool charactersMatch = source[i - 1] == target[j - 1];
                int substitutionCost = charactersMatch ? 0 : 1;

                dp[i, j] = Math.Min(
                    Math.Min(
                        dp[i - 1, j] + 1,        // deletion
                        dp[i, j - 1] + 1),       // insertion
                    dp[i - 1, j - 1] + substitutionCost // substitution
                );
            }
        }

        return dp[sourceLength, targetLength];
    }
}
