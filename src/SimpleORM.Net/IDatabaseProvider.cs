using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Low-level database provider contract.</summary>
public interface IDatabaseProvider
{
    /// <summary>Begins a native database transaction.</summary>
    Task<IDBTransaction> BeginTransaction(
        CancellationToken cancellationToken = default);

    /// <summary>Selects one matching record.</summary>
    Task<T?> SelectSingle<T>(
        SearchParam search,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Selects one record by its globally unique Code.</summary>
    Task<T?> GetByCode<T>(
        string code,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Selects one physical result batch.</summary>
    Task<IReadOnlyList<T>> Select<T>(
        SearchParam search,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Selects one physical result batch containing only explicitly selected fields.</summary>
    Task<IReadOnlyList<dynamic>> SelectDynamic<T>(
        SearchParam search,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Counts matching records before pagination.</summary>
    Task<long> Count<T>(
        SearchParam search,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Inserts one physical batch.</summary>
    Task Insert<T>(
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Updates one physical batch.</summary>
    Task Update<T>(
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>Deletes one physical batch.</summary>
    Task Delete<T>(
        IReadOnlyList<T> models,
        bool hardDelete,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Physically deletes records older than the supplied UTC cutoff. This operation
    /// intentionally bypasses tenant scoping because it is system-level retention work.
    /// </summary>
    Task<long> DeleteStale(
        DBModelMetadata metadata,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Renders a provider-specific query for debugging.</summary>
    string GenerateDebugQuery<T>(
        SearchParam search,
        int skip = 0,
        int limit = 100)
        where T : DBModel;
}
