using MongoDB.Driver;
using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// Wraps a MongoDB client session transaction behind the provider-neutral
/// <see cref="IDBTransaction"/> contract.
/// </summary>
internal sealed class MongoTx : IDBTransaction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MongoTx"/> class.
    /// </summary>
    /// <param name="session">The MongoDB client session.</param>
    public MongoTx(
        IClientSessionHandle session)
    {
        Session = session;
    }

    /// <summary>
    /// Gets the underlying MongoDB client session.
    /// </summary>
    public IClientSessionHandle Session { get; }

    /// <inheritdoc />
    public async Task Commit(
        CancellationToken cancellationToken = default)
    {
        await Session.CommitTransactionAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task Rollback(
        CancellationToken cancellationToken = default)
    {
        await Session.AbortTransactionAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Session.Dispose();
        return ValueTask.CompletedTask;
    }
}