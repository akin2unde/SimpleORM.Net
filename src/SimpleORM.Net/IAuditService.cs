using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Audit service contract.</summary>
public interface IAuditService
{

    /// <summary>Writes an operation audit.</summary>
    Task Write<T>(string action,IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;


}
