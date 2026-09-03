using SimpleORM.Net;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.AspNetCore;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.MongoDB;
using SimpleORM.Net.Services;
using SimpleORM.Net.SqlServer;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Sample application entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Builds, configures, and runs the sample API.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the application.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var provider = ResolveProvider(
            builder.Configuration["SimpleOrm:Provider"]);

        builder.Services.AddSimpleOrm(
            options => ConfigureSimpleOrm(
                options,
                builder.Configuration,
                provider),
            typeof(Customer).Assembly,
            typeof(SimpleORM.Net.SystemModels.DBExtensionDefinition).Assembly);

        if (provider == DatabaseType.MongoDb)
        {
            builder.Services.AddSimpleOrmMongoDB();
        }
        else
        {
            builder.Services.AddSimpleOrmSqlServer();
        }

        builder.Services.AddSimpleOrmAspNetCore();

        // AddSimpleOrm registers DefaultExtensionService. The sample replaces it
        // with a database-backed implementation so Customer.Extended is demonstrated
        // end to end without changing the core public contracts.
        builder.Services.AddScoped<IExtensionService, SampleExtensionService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();

        builder.Services.AddControllers();

        var app = builder.Build();

        await SynchronizeSchema(
            app.Services,
            app.Lifetime.ApplicationStopping);

        var simpleOrmOptions = app.Services.GetRequiredService<SimpleOrmOptions>();

        if (simpleOrmOptions.ErrorLog.Enabled)
        {
            app.UseSimpleOrmErrors();
        }

        app.MapControllers();

        await app.RunAsync();
    }

    private static DatabaseType ResolveProvider(string? provider)
    {
        return provider?.Equals(
            "MongoDb",
            StringComparison.OrdinalIgnoreCase) == true
            ? DatabaseType.MongoDb
            : DatabaseType.SqlServer;
    }

    private static void ConfigureSimpleOrm(
        SimpleOrmOptions options,
        IConfiguration configuration,
        DatabaseType provider)
    {
        options.Database = provider;
        options.Connection.Host = configuration["SimpleOrm:Host"] ?? "localhost";
        options.Connection.Port = ResolvePort(
            configuration["SimpleOrm:Port"],
            provider);
        options.Connection.Database = configuration["SimpleOrm:Database"]
            ?? "SimpleOrmSample";
        options.Connection.Username = configuration["SimpleOrm:Username"];
        options.Connection.Password = configuration["SimpleOrm:Password"];

        options.DefaultStringLength = 50;
        options.EnumStorage = EnumStorage.String;
        options.CodeGeneration.Length = 10;
        options.Batch.Save = 100;
        options.Batch.Select = 100;
        options.AutoMigration = true;

        options.Extensions.RequirePublish = configuration.GetValue(
            "SimpleOrm:Extensions:RequirePublish",
            false);

        options.MultiTenancy.Enabled = configuration.GetValue(
            "SimpleOrm:MultiTenancy:Enabled",
            false);

        options.MultiTenancy.JwtClaim = configuration[
            "SimpleOrm:MultiTenancy:JwtClaim"] ?? "tenant";

        options.Search.IncludeTenantCode = configuration.GetValue(
            "SimpleOrm:Search:IncludeTenantCode",
            false);

        options.AuditTrail.Enabled = configuration.GetValue(
            "SimpleOrm:AuditTrail:Enabled",
            false);

        options.ErrorLog.Enabled = configuration.GetValue(
            "SimpleOrm:ErrorLog:Enabled",
            false);

        options.ErrorLog.AutoDeleteEnabled = configuration.GetValue(
            "SimpleOrm:ErrorLog:AutoDeleteEnabled",
            false);

        options.ErrorLog.RetentionDays = configuration.GetValue(
            "SimpleOrm:ErrorLog:RetentionDays",
            60);

        options.ErrorLog.CleanupCron = configuration[
            "SimpleOrm:ErrorLog:CleanupCron"] ?? "0 0 1 * *";
    }

    private static int ResolvePort(
        string? configuredPort,
        DatabaseType provider)
    {
        if (int.TryParse(
                configuredPort,
                out var port))
        {
            return port;
        }

        return provider == DatabaseType.MongoDb
            ? 27017
            : 1433;
    }

    private static async Task SynchronizeSchema(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();

        var metadata = scope.ServiceProvider.GetRequiredService<IDBMetadataProvider>();
        var registry = scope.ServiceProvider.GetRequiredService<ModelAssemblyRegistry>();

        foreach (var modelType in ModelDiscovery.Discover(registry.Assemblies))
        {
            metadata.RegisterModel(modelType);
        }

        var synchronizer = scope.ServiceProvider.GetRequiredService<IDBSchemaSynchronizer>();

        await synchronizer.Synchronize(
            cancellationToken);
    }
}
