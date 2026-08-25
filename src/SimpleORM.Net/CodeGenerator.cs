using System.Linq.Expressions;

using System.Security.Cryptography;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>Cryptographically random uppercase alphanumeric code generator.</summary>
public sealed class CodeGenerator(IDBMetadataProvider metadata, SimpleOrmOptions options) : ICodeGenerator
{

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <inheritdoc />
    public string Generate<T>() where T : DBModel
    {
        var m = metadata.GetMetadata<T>();

        return $"{m.CodePrefix}{options.CodeGeneration.Separator}{RandomNumberGenerator.GetString(Alphabet, m.CodeLength)}";

    }
    /// <summary>
    /// Generate Code on the fly
    /// </summary>
    ///  /// <param name="prefix"></param>
    /// <typeparam name="T"></typeparam>
    /// <param name="length"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static string GenerateCode<T>(string prefix, int length, string separator) where T : DBModel
    {

        return $"{prefix}{separator}{RandomNumberGenerator.GetString(Alphabet, length)}";
    }

}
