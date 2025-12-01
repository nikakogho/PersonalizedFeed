using PersonalizedFeed.Domain.Repositories;

namespace PersonalizedFeed.Infrastructure.InMemory.Repositories;

public sealed class InMemorySystemConfigRepository : ISystemConfigRepository
{
    private readonly InMemoryDataStore _store;

    public InMemorySystemConfigRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<bool> IsPersonalizationGloballyEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.PersonalizationEnabledGlobally);
    }
}
