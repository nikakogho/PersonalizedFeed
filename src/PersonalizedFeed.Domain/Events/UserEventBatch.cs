namespace PersonalizedFeed.Domain.Events;

public sealed record UserEventBatch(
    string TenantId,
    string UserHash,
    IReadOnlyList<UserEvent> Events);
