using Microsoft.Extensions.DependencyInjection;
using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// MongoDB provider registration helpers.
/// </summary>
public static class MongoRegistration
{
    /// <summary>
    /// Registers the MongoDB provider and MongoDB index synchronizer.
    /// </summary>
    public static IServiceCollection AddSimpleOrmMongoDB(
        this IServiceCollection services)
    {
        // MongoDatabaseProvider depends on ITenantProvider, which is scoped for
        // request-aware multi-tenancy. The provider therefore must not be singleton.
        services.AddScoped<MongoDatabaseProvider>();

        services.AddScoped<IDatabaseProvider>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBQuery>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBSchemaSynchronizer, MongoIndexSynchronizer>();

        return services;
    }
}
