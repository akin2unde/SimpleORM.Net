using System.Reflection;


using Microsoft.Extensions.DependencyInjection;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Services;


namespace SimpleORM.Net;


/// <summary>Core DI helpers.</summary>
public static class DependencyInjection
{

    /// <summary>Registers provider-neutral services. Register a provider package after this call.</summary>
    public static IServiceCollection AddSimpleOrmCore(this IServiceCollection services,Action<SimpleOrmOptions> configure,params Assembly[] modelAssemblies)
    {

        var o=new SimpleOrmOptions();

        configure(o);

        if(o.Connection.Port<=0)throw new InvalidOperationException("Connection.Port must be > 0.");

        if(string.IsNullOrWhiteSpace(o.Connection.Database))throw new InvalidOperationException("Connection.Database is required.");

        if(o.CodeGeneration.Length<4)throw new InvalidOperationException("Code length must be >= 4.");


        services.AddSingleton(o);

        services.AddSingleton<IDBMetadataProvider,DBMetadataProvider>();

        services.AddSingleton<ICodeGenerator,CodeGenerator>();

        services.AddScoped<IDBTransactionManager,DBTransactionManager>();

        services.AddScoped<IExtensionService,DefaultExtensionService>();

        services.AddScoped<IAuditService,DefaultAuditService>();

        services.AddScoped(typeof(IDataService<>),typeof(DataService<>));

        services.AddSingleton(new ModelAssemblyRegistry(modelAssemblies.Length==0?AppDomain.CurrentDomain.GetAssemblies():modelAssemblies));

        return services;


    }

    /// <summary>Adds null tenant/user providers for non-ASP.NET hosts.</summary>
    public static IServiceCollection AddSimpleOrmDefaultIdentityProviders(this IServiceCollection services)
    {
        services.AddScoped<ITenantProvider,NullTenant>();

        services.AddScoped<IUserProvider,NullUser>();

        return services;

    }

}
