using PersonalizedFeed.Domain.Models;

namespace PersonalizedFeed.Domain.Repositories;

public interface IUserSignalsRepository
{
    Task<UserSignals?> GetByTenantAndUserHashAsync(
        string tenantId,
        string userHash,
        CancellationToken cancellationToken = default);
}
