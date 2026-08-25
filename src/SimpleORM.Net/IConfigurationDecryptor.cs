using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Decrypts ENC:-protected configuration values.</summary>
public interface IConfigurationDecryptor
{

    /// <summary>Decrypts.</summary>
    string Decrypt(string encryptedValue);

}
