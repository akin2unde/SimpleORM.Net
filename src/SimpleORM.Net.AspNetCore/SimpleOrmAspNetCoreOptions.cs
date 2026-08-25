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

/// <summary>ASP.NET-specific options.</summary>
public sealed class SimpleOrmAspNetCoreOptions
{

    /// <summary>User JWT claim.</summary>
    public string UserClaim
    {
        get;

        set;

    }
    ="sub";

    /// <summary>Payload encryption options.</summary>
    public PayloadEncryptionOptions Encryption
    {
        get;

    }
    =new();

}
