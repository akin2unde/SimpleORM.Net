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


/// <summary>Optional hybrid encryption configuration.</summary>
public sealed class PayloadEncryptionOptions
{

    /// <summary>Enable encryption middleware.</summary>
    public bool Enabled
    {
        get;

        set;

    }

    /// <summary>Encrypted-request marker header.</summary>
    public string HeaderName
    {
        get;

        set;

    }
    ="X-SimpleORM-Encrypted";


    /// <summary>RSA public key PEM.</summary>
    public string? PublicKey
    {
        get;

        set;

    }

    /// <summary>RSA private key PEM.</summary>
    public string? PrivateKey
    {
        get;

        set;

    }

}
