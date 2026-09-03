using Cronos;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Attributes;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Services;

namespace SimpleORM.Net;

/// <summary>Dependency injection helpers for SimpleORM.Net.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers provider-neutral SimpleORM services. Register the selected database
    /// provider package separately with AddSimpleOrmSqlServer or AddSimpleOrmMongoDB.
    /// </summary>
    public static IServiceCollection AddSimpleOrm(
        this IServiceCollection services,
        Action<SimpleOrmOptions> configure,
        params Assembly[] modelAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SimpleOrmOptions();
        configure(options);

        if (options.Connection.Port <= 0)
        {
            throw new InvalidOperationException("Connection.Port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.Connection.Database))
        {
            throw new InvalidOperationException("Connection.Database is required.");
        }

        if (options.CodeGeneration.Length < 4)
        {
            throw new InvalidOperationException("Code length must be at least 4.");
        }

        if (options.ErrorLog.AutoDeleteEnabled)
        {
            if (options.ErrorLog.RetentionDays <= 0)
            {
                throw new InvalidOperationException(
                    "ErrorLog.RetentionDays must be greater than zero when auto delete is enabled.");
            }

            _ = CronExpression.Parse(
                options.ErrorLog.CleanupCron,
                CronFormat.Standard);
        }

        services.AddSingleton(options);
        services.AddSingleton<IDBMetadataProvider, DBMetadataProvider>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IDBTransactionManager, DBTransactionManager>();
        services.AddScoped<IExtensionService, DefaultExtensionService>();
        services.AddScoped<IAuditService, DefaultAuditService>();
        services.AddScoped<IDataRepository, DataRepository>();

        var assemblies = modelAssemblies.Length == 0
            ? AppDomain.CurrentDomain.GetAssemblies().ToList()
            : modelAssemblies.ToList();

        var simpleOrmAssembly = typeof(DependencyInjection).Assembly;

        if (!assemblies.Contains(simpleOrmAssembly))
        {
            assemblies.Add(simpleOrmAssembly);
        }

        var hasModelCleanup = ModelDiscovery
            .Discover(assemblies)
            .Any(type => type.IsDefined(
                typeof(AutoDeleteAttribute),
                inherit: true));

        if (options.ErrorLog.AutoDeleteEnabled || hasModelCleanup)
        {
            services.AddHostedService<StaleDataCleanupHostedService>();
        }

        services.AddSingleton(
            new ModelAssemblyRegistry(assemblies));

        return services;
    }

    /// <summary>Adds null tenant/user providers for non-ASP.NET hosts.</summary>
    public static IServiceCollection AddSimpleOrmDefaultIdentityProviders(
        this IServiceCollection services)
    {
        services.AddScoped<ITenantProvider, NullTenant>();
        services.AddScoped<IUserProvider, NullUser>();
        return services;
    }
}
