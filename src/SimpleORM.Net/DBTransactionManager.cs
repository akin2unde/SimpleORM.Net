using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net.Services;

/// <summary>Scoped transaction manager that reuses an existing transaction.</summary>
public sealed class DBTransactionManager(IDatabaseProvider provider) : IDBTransactionManager
{
    private IDBTransaction? _current;

    /// <inheritdoc />
    public bool HasTransaction => _current is not null;

    /// <inheritdoc />
    public IDBTransaction? Current => _current;

    /// <inheritdoc />
    public async Task Execute(
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        await Execute(
            async () =>
            {
                await action();
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResult> Execute<TResult>(
        Func<Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            return await action();
        }

        await using var transaction = await provider.BeginTransaction(cancellationToken);
        _current = transaction;

        try
        {
            var result = await action();
            await transaction.Commit(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.Rollback(cancellationToken);
            }
            catch
            {
                // Preserve the original operation exception.
            }

            throw;
        }
        finally
        {
            _current = null;
        }
    }
}
