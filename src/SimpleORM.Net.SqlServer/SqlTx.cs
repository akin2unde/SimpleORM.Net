using Microsoft.Data.SqlClient;
using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net.SqlServer;

/// <summary>
/// Wraps a SQL Server connection and transaction behind the provider-neutral
/// <see cref="IDBTransaction"/> contract.
/// </summary>
internal sealed class SqlTx : IDBTransaction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTx"/> class.
    /// </summary>
    /// <param name="connection">The open SQL Server connection.</param>
    /// <param name="transaction">The active SQL Server transaction.</param>
    public SqlTx(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    /// <summary>
    /// Gets the SQL Server connection that owns the transaction.
    /// </summary>
    public SqlConnection Connection { get; }

    /// <summary>
    /// Gets the underlying SQL Server transaction.
    /// </summary>
    public SqlTransaction Transaction { get; }

    /// <inheritdoc />
    public async Task Commit(
        CancellationToken cancellationToken = default)
    {
        await Transaction.CommitAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task Rollback(
        CancellationToken cancellationToken = default)
    {
        await Transaction.RollbackAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }
}