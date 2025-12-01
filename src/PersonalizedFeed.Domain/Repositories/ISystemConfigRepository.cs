namespace PersonalizedFeed.Domain.Repositories;

public interface ISystemConfigRepository
{
    Task<bool> IsPersonalizationGloballyEnabledAsync(
        CancellationToken cancellationToken = default);
}
