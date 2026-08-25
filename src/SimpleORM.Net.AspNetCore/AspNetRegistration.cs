using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using Microsoft.AspNetCore.Builder;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.AspNetCore;

/// <summary>ASP.NET registration helpers.</summary>
public static class AspNetRegistration
{

    /// <summary>Registers JWT identity providers and options.</summary>
    public static IServiceCollection AddSimpleOrmAspNetCore(this IServiceCollection s,Action<SimpleOrmAspNetCoreOptions>? configure=null)
    {
        var o=new SimpleOrmAspNetCoreOptions();

        configure?.Invoke(o);

        s.AddSingleton(o);

        s.AddHttpContextAccessor();

        s.AddScoped<ITenantProvider,JwtTenantProvider>();

        s.AddScoped<IUserProvider,JwtUserProvider>();

        return s;

    }

    /// <summary>Adds optional error middleware.</summary>
    public static IApplicationBuilder UseSimpleOrmErrors(this IApplicationBuilder a)=>a.UseMiddleware<SimpleOrmErrorMiddleware>();

    /// <summary>Adds optional encryption middleware.</summary>
    public static IApplicationBuilder UseSimpleOrmEncryption(this IApplicationBuilder a)=>a.UseMiddleware<SimpleOrmEncryptionMiddleware>();

}
