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

/// <summary>Optional hybrid RSA/AES middleware.</summary>
public sealed class SimpleOrmEncryptionMiddleware(RequestDelegate next)
{

    /// <summary>Invokes middleware.</summary>
    public async Task InvokeAsync(HttpContext context,SimpleOrmAspNetCoreOptions o)
    {

        if(!o.Encryption.Enabled||!context.Request.Headers.TryGetValue(o.Encryption.HeaderName,out var h)||!string.Equals(h.ToString(),"true",StringComparison.OrdinalIgnoreCase))
        {
            await next(context);

            return;

        }

        if(string.IsNullOrWhiteSpace(o.Encryption.PublicKey)||string.IsNullOrWhiteSpace(o.Encryption.PrivateKey))throw new InvalidOperationException("Encryption keys are required.");

        if(context.Request.ContentLength is >0){var json=await new StreamReader(context.Request.Body).ReadToEndAsync();

        var env=JsonSerializer.Deserialize<EncryptionEnvelope>(json)??throw new InvalidOperationException("Invalid encrypted envelope.");

        var plain=Decrypt(env,o.Encryption.PrivateKey);

        context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(plain));

        context.Request.ContentLength=context.Request.Body.Length;

    }

    var original=context.Response.Body;

    await using var buffer=new MemoryStream();

    context.Response.Body=buffer;

    await next(context);

    buffer.Position=0;

    var response=await new StreamReader(buffer).ReadToEndAsync();

    var output=Encrypt(response,o.Encryption.PublicKey);

    context.Response.Body=original;

    context.Response.ContentType="application/json";

    await context.Response.WriteAsync(JsonSerializer.Serialize(output));

}

private static EncryptionEnvelope Encrypt(string text,string pem)
{
    using var aes=Aes.Create();

    aes.GenerateKey();

    aes.GenerateIV();

    using var e=aes.CreateEncryptor();

    var bytes=Encoding.UTF8.GetBytes(text);

    var data=e.TransformFinalBlock(bytes,0,bytes.Length);

    using var rsa=RSA.Create();

    rsa.ImportFromPem(pem);

    return new EncryptionEnvelope(Convert.ToBase64String(rsa.Encrypt(aes.Key,RSAEncryptionPadding.OaepSHA256)),Convert.ToBase64String(aes.IV),Convert.ToBase64String(data));

}

private static string Decrypt(EncryptionEnvelope env,string pem)
{
    using var rsa=RSA.Create();

    rsa.ImportFromPem(pem);

    var key=rsa.Decrypt(Convert.FromBase64String(env.Key),RSAEncryptionPadding.OaepSHA256);

    using var aes=Aes.Create();

    aes.Key=key;

    aes.IV=Convert.FromBase64String(env.Iv);

    using var d=aes.CreateDecryptor();

    var data=Convert.FromBase64String(env.Data);

    return Encoding.UTF8.GetString(d.TransformFinalBlock(data,0,data.Length));

}

}
