namespace SimpleORM.Net.Abstractions;

/// <summary>Scoped transaction coordinator.</summary>
public interface IDBTransactionManager
{
    /// <summary>Gets whether a transaction is currently active.</summary>
    bool HasTransaction { get; }

    /// <summary>Gets the current transaction, when one exists.</summary>
    IDBTransaction? Current { get; }

    /// <summary>Runs an action atomically, reusing an existing transaction.</summary>
    Task Execute(
        Func<Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a function atomically and returns its result.</summary>
    Task<TResult> Execute<TResult>(
        Func<Task<TResult>> action,
        CancellationToken cancellationToken = default);
}
