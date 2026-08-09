using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Primary CRUD/query API.</summary>
public interface IDataService<T> where T:DBModel
{

    /// <summary>Selects; limit 0 means all remaining.</summary>
    Task<PagedResult<T>> Select(SearchParam? search=null,int skip=0,int limit=100,int? batch=null,CancellationToken cancellationToken=default);


    /// <summary>Expression select.</summary>
    Task<PagedResult<T>> Select(Expression<Func<T,bool>> expression,SearchParam? search=null,int skip=0,int limit=100,int? batch=null,CancellationToken cancellationToken=default);


    /// <summary>One record.</summary>
    Task<T?> SelectSingle(SearchParam? search=null,CancellationToken cancellationToken=default);


    /// <summary>One record by expression.</summary>
    Task<T?> SelectSingle(Expression<Func<T,bool>> expression,SearchParam? search=null,CancellationToken cancellationToken=default);


    /// <summary>By Code.</summary>
    Task<T?> GetByCode(string code,CancellationToken cancellationToken=default);


    /// <summary>Generic text search across eligible strings.</summary>
    Task<PagedResult<T>> Search(string search,SearchParam? searchParam=null,int skip=0,int limit=100,int? batch=null,CancellationToken cancellationToken=default);


    /// <summary>Count.</summary>
    Task<long> Count(SearchParam? search=null,CancellationToken cancellationToken=default);


    /// <summary>Expression count.</summary>
    Task<long> Count(Expression<Func<T,bool>> expression,SearchParam? search=null,CancellationToken cancellationToken=default);


    /// <summary>Single Save driven by DataState.</summary>
    Task<T> Save(T model,CancellationToken cancellationToken=default);


    /// <summary>Batch Save driven by DataState.</summary>
    Task<IReadOnlyList<T>> Save(IEnumerable<T> models,int? batch=null,CancellationToken cancellationToken=default);


    /// <summary>Debug query with embedded values; never used for execution.</summary>
    string GenerateDebugQuery(SearchParam? search=null,int skip=0,int limit=100);


}
