using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Abstractions;


/// <summary>Raw provider query escape hatch; normal tenant/soft-delete protections may be bypassed.</summary>
public interface IDBQuery
{

    /// <summary>One dynamic row.</summary>
    Task<dynamic?> QuerySingle(string query,object? parameters=null,CancellationToken cancellationToken=default);


    /// <summary>Dynamic rows.</summary>
    Task<IReadOnlyList<dynamic>> Query(string query,object? parameters=null,CancellationToken cancellationToken=default);


    /// <summary>One typed DTO row.</summary>
    Task<T?> QuerySingle<T>(string query,object? parameters=null,CancellationToken cancellationToken=default);


    /// <summary>Typed DTO rows.</summary>
    Task<IReadOnlyList<T>> Query<T>(string query,object? parameters=null,CancellationToken cancellationToken=default);


    /// <summary>Non-query affected rows.</summary>
    Task<long> Execute(string query,object? parameters=null,CancellationToken cancellationToken=default);


}
