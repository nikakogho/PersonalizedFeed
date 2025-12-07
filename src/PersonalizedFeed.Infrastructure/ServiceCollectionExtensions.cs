using Microsoft.Extensions.DependencyInjection;
using PersonalizedFeed.Domain.Repositories;
using PersonalizedFeed.Infrastructure.InMemory;
using PersonalizedFeed.Infrastructure.InMemory.Repositories;

namespace PersonalizedFeed.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryDataStore>();

        services.AddScoped<ITenantConfigRepository, InMemoryTenantConfigRepository>();
        services.AddScoped<ISystemConfigRepository, InMemorySystemConfigRepository>();
        services.AddScoped<IUserSignalsRepository, InMemoryUserSignalsRepository>();
        services.AddScoped<IVideoRepository, InMemoryVideoRepository>();

        services.AddDistributedMemoryCache();

        return services;
    }
}
