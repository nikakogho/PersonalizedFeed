namespace PersonalizedFeed.Domain.Models;

public sealed record TenantConfig
{
    public required string TenantId { get; init; }
    public required string ApiKey { get; init; }

    public bool UsePersonalization { get; init; } = true;
    public int DefaultLimit { get; init; } = 20;
    public string MaturityPolicy { get; init; } = "PG13";

    public string RankingModelType { get; init; } = "linear";
    public string RankingModelVersion { get; init; } = "1.0.0";
    public string? RankingModelPayloadJson { get; init; }

    public string? FeatureFlagsJson { get; init; }
}
