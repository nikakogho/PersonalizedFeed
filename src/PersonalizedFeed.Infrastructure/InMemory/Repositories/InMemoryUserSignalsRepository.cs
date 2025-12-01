using PersonalizedFeed.Domain.Models;
using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Infrastructure.InMemory.Repositories;

public sealed class InMemoryUserSignalsRepository : IUserSignalsRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryUserSignalsRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<UserSignals?> GetByTenantAndUserHashAsync(
        string tenantId,
        string userHash,
        CancellationToken cancellationToken = default)
    {
        var key = (tenantId, userHash);
        _store.UserSignals.TryGetValue(key, out var signals);
        return Task.FromResult<UserSignals?>(signals);
    }
}
