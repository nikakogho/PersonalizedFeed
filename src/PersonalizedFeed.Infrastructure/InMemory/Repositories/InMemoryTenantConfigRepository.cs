using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Infrastructure.InMemory.Repositories;

public sealed class InMemoryTenantConfigRepository : ITenantConfigRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryTenantConfigRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<TenantConfig?> GetByTenantIdAndApiKeyAsync(
        string tenantId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (!_store.Tenants.TryGetValue(tenantId, out var tenant))
            return Task.FromResult<TenantConfig?>(null);

        if (!string.Equals(tenant.ApiKey, apiKey, StringComparison.Ordinal))
            return Task.FromResult<TenantConfig?>(null);

        return Task.FromResult<TenantConfig?>(tenant);
    }

    public Task SetPersonalizationAsync(string tenantId, string apiKey, bool enable, CancellationToken cancellationToken = default)
    {
        if (!_store.Tenants.TryGetValue(tenantId, out var tenant))
            throw new ArgumentException($"Tenant with ID '{tenantId}' does not exist.", nameof(tenantId));

        if (!string.Equals(tenant.ApiKey, apiKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid API key.");

        var updatedTenant = tenant with { UsePersonalization = enable };

        _store.Tenants[tenantId] = updatedTenant;

        return Task.CompletedTask;
    }
}
