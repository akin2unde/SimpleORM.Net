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


/// <summary>JWT user resolver.</summary>
public sealed class JwtUserProvider(IHttpContextAccessor h,SimpleOrmAspNetCoreOptions o):IUserProvider
{

    /// <inheritdoc />
    public string? GetUserCode()=>h.HttpContext?.User?.FindFirst(o.UserClaim)?.Value;


}
