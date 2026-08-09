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


/// <summary>JWT tenant resolver.</summary>
public sealed class JwtTenantProvider(IHttpContextAccessor h,SimpleOrmOptions o):ITenantProvider
{

    /// <inheritdoc />
    public string? GetTenantCode()=>h.HttpContext?.User?.FindFirst(o.MultiTenancy.JwtClaim)?.Value;


}
