using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Provider-neutral transaction.</summary>
public interface IDBTransaction:IAsyncDisposable
{

    /// <summary>Commit.</summary>
    Task Commit(CancellationToken cancellationToken=default);

    /// <summary>Rollback.</summary>
    Task Rollback(CancellationToken cancellationToken=default);

}
