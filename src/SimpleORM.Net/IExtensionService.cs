using System.Linq.Expressions;

using System.Security.Cryptography;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>Dynamic extension service contract.</summary>
public interface IExtensionService
{

    /// <summary>Load model extension values.</summary>
    Task Load<T>(IReadOnlyList<T> models,CancellationToken cancellationToken=default) where T:DBModel;

    /// <summary>Save extension values in business transaction.</summary>
    Task Save<T>(IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;

}
