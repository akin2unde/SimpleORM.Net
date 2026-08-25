using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Services;

/// <summary>No-op extension service used when model extension persistence is not customized.</summary>
public sealed class DefaultExtensionService:IExtensionService
{

    /// <inheritdoc />
    public Task Load<T>(IReadOnlyList<T> models,CancellationToken cancellationToken=default) where T:DBModel=>Task.CompletedTask;

    /// <inheritdoc />
    public Task Save<T>(IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel=>Task.CompletedTask;

}
