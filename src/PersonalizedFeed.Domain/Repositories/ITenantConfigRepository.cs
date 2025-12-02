using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Repositories;

public interface ITenantConfigRepository
{
    Task<TenantConfig?> GetByTenantIdAndApiKeyAsync(
        string tenantId,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task SetPersonalizationAsync(
        string tenantId,
        string apiKey,
        bool enable,
        CancellationToken cancellationToken = default);
}
