using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Abstractions;


/// <summary>Scoped transaction coordinator.</summary>
public interface IDBTransactionManager
{

    /// <summary>Whether active.</summary>
    bool HasTransaction
    {
        get;

    }

    /// <summary>Current transaction.</summary>
    IDBTransaction? Current
    {
        get;

    }

    /// <summary>Runs action atomically, reusing existing transaction.</summary>
    Task Execute(Func<Task> action,CancellationToken cancellationToken=default);


    /// <summary>Runs function atomically.</summary>
    Task<TResult> Execute<TResult>(Func<Task<TResult>> action,CancellationToken cancellationToken=default);


}
