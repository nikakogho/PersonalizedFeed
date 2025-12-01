namespace PersonalizedFeed.Domain.Services.Models;

public sealed record FeedRequest(
    string TenantId,
    string ApiKey,
    string UserHash,
    int? Limit);
