using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Scoped transaction manager.</summary>
public sealed class DBTransactionManager(IDatabaseProvider provider):IDBTransactionManager
{

    private IDBTransaction? _current;


    /// <inheritdoc />
    public bool HasTransaction=>_current is not null;


    /// <inheritdoc />
    public IDBTransaction? Current=>_current;


    /// <inheritdoc />
    public async Task Execute(Func<Task> action,CancellationToken ct=default)=>_=await Execute(async()=>
    {
        await action();
return true;

    }
    ,ct);


    /// <inheritdoc />
    public async Task<TResult> Execute<TResult>(Func<Task<TResult>> action,CancellationToken ct=default)
    {
        if(_current is not null)return await action();

        await using var tx=await provider.BeginTransaction(ct);

        _current=tx;

        try
        {
            var r=await action();

            await tx.Commit(ct);

            return r;

        }
        catch
        {
            try
            {
                await tx.Rollback(ct);

            }
            catch
            {
            }
            throw;

        }
        finally
        {
            _current=null;

        }
    }

}
