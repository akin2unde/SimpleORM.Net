using Microsoft.Extensions.DependencyInjection;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.MongoDB.Configuration;
using SimpleORM.Net.MongoDB.Options;

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
        this IServiceCollection services, Action<MongoDBOptions>? configure = null)
    {
        var options = new MongoDBOptions();

        configure?.Invoke(options);

        MongoDBConventionRegistrar.Register(options);

        services.AddSingleton(options);
        // MongoDatabaseProvider depends on ITenantProvider, which is scoped for
        // request-aware multi-tenancy. The provider therefore must not be singleton.
        services.AddScoped<MongoDatabaseProvider>();

        services.AddScoped<IDatabaseProvider>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBQuery>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBSchemaSynchronizer, MongoIndexSynchronizer>();


        // Existing MongoDB registrations remain here.

        return services;
    }
}
