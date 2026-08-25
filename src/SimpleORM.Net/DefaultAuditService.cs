using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Services;

/// <summary>No-op audit service; provider-specific persistent audit can replace it.</summary>
public sealed class DefaultAuditService:IAuditService
{

    /// <inheritdoc />
    public Task Write<T>(string action,IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel=>Task.CompletedTask;

}
