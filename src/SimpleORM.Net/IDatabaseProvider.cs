using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Abstractions;


/// <summary>Low-level provider contract.</summary>
public interface IDatabaseProvider
{

    /// <summary>Begin native transaction.</summary>
    Task<IDBTransaction> BeginTransaction(CancellationToken cancellationToken=default);


    /// <summary>One record.</summary>
    Task<T?> SelectSingle<T>(SearchParam search,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>By globally unique Code.</summary>
    Task<T?> GetByCode<T>(string code,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Physical select batch.</summary>
    Task<IReadOnlyList<T>> Select<T>(SearchParam search,int skip,int limit,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Count before pagination.</summary>
    Task<long> Count<T>(SearchParam search,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Insert physical batch.</summary>
    Task Insert<T>(IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Update physical batch.</summary>
    Task Update<T>(IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Delete physical batch.</summary>
    Task Delete<T>(IReadOnlyList<T> models,bool hardDelete,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;


    /// <summary>Debug-only rendered provider query.</summary>
    string GenerateDebugQuery<T>(SearchParam search,int skip=0,int limit=100) where T:DBModel;


}
