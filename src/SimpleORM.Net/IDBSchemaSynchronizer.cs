using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Provider startup schema/index synchronization.</summary>
public interface IDBSchemaSynchronizer
{

    /// <summary>Synchronizes.</summary>
    Task Synchronize(CancellationToken cancellationToken=default);

}
