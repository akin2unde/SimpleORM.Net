using System.Linq.Expressions;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>Default repository implementation for all DBModel types.</summary>
public sealed class DataRepository(
    IDatabaseProvider provider,
    IDBMetadataProvider metadata,
    IDBTransactionManager transactionManager,
    ITenantProvider tenantProvider,
    IUserProvider userProvider,
    IExtensionService extensions,
    IAuditService audit,
    ISearchParamNormalizer searchNormalizer,
    SimpleOrmOptions options) : IDataRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<T>> Select<T>(SearchParam? search = null, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null) where T : DBModel
    {
        if (skip < 0 || limit < 0) throw new ArgumentOutOfRangeException();
        var param = searchNormalizer.Normalize<T>(search);
        var total = await provider.Count<T>(param, cancellationToken);
        var available = Math.Max(0, total - skip);
        var target = limit == 0 ? available : Math.Min(limit, available);
        var batchSize = BatchResolver.Resolve(batch, options.Batch.Select);
        var data = new List<T>();
        var position = skip;
        long remaining = target;
        while (remaining > 0)
        {
            var take = (int)Math.Min(batchSize, remaining);
            var chunk = await provider.Select<T>(param, position, take, cancellationToken);
            if (chunk.Count == 0) break;
            data.AddRange(chunk);
            position += chunk.Count;
            remaining -= chunk.Count;
            if (chunk.Count < take) break;
        }
        await extensions.Load(data, cancellationToken);
        ApplyDefaults(data);
        return new PagedResult<T> { Data = data, TotalRecords = total, Skipped = skip, Limit = limit };
    }

    /// <inheritdoc />
    public Task<PagedResult<T>> Select<T>(
        int skip,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel =>
        Select<T>(
            search: null,
            skip: skip,
            limit: limit,
            cancellationToken: cancellationToken,
            batch: batch);

    /// <inheritdoc />
    public Task<PagedResult<T>> Select<T>(Expression<Func<T, bool>> expression, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null) where T : DBModel =>
        Select(expression, null, skip, limit, cancellationToken, batch);
    /// <inheritdoc />
    public Task<PagedResult<T>> Select<T>(Expression<Func<T, bool>> expression, SearchParam? search, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null) where T : DBModel =>
        Select<T>(ExpressionTranslator.Translate(expression, search), skip, limit, cancellationToken, batch);

    /// <inheritdoc />
    public async Task<PagedResult<dynamic>> SelectDynamic<T>(
        SearchParam search,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        if (skip < 0 || limit < 0)
        {
            throw new ArgumentOutOfRangeException(
                skip < 0 ? nameof(skip) : nameof(limit));
        }

        var param = searchNormalizer.Normalize<T>(search);

        if (param.Fields.Count == 0
            && param.Joins.All(join => join.Fields.Count == 0))
        {
            throw new InvalidOperationException(
                "SelectDynamic requires at least one selected field.");
        }

        var total = await provider.Count<T>(
            param,
            cancellationToken);

        var available = Math.Max(0, total - skip);
        var target = limit == 0
            ? available
            : Math.Min(limit, available);

        var batchSize = BatchResolver.Resolve(
            batch,
            options.Batch.Select);

        var data = new List<dynamic>();
        var position = skip;
        long remaining = target;

        while (remaining > 0)
        {
            var take = (int)Math.Min(batchSize, remaining);
            var chunk = await provider.SelectDynamic<T>(
                param,
                position,
                take,
                cancellationToken);

            if (chunk.Count == 0)
            {
                break;
            }

            data.AddRange(chunk);
            position += chunk.Count;
            remaining -= chunk.Count;

            if (chunk.Count < take)
            {
                break;
            }
        }

        return new PagedResult<dynamic>
        {
            Data = data,
            TotalRecords = total,
            Skipped = skip,
            Limit = limit
        };
    }

    /// <inheritdoc />
    public async Task<T?> SelectSingle<T>(SearchParam? search = null, CancellationToken cancellationToken = default) where T : DBModel
    {
        var item = await provider.SelectSingle<T>(searchNormalizer.Normalize<T>(search), cancellationToken);
        if (item is null) return null;
        await extensions.Load(new[] { item }, cancellationToken);
        ApplyDefaults(new[] { item });
        return item;
    }

    /// <inheritdoc />
    public Task<T?> SelectSingle<T>(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default) where T : DBModel =>
        SelectSingle(expression, null, cancellationToken);

    /// <inheritdoc />
    public Task<T?> SelectSingle<T>(Expression<Func<T, bool>> expression, SearchParam? search, CancellationToken cancellationToken = default) where T : DBModel =>
        SelectSingle<T>(ExpressionTranslator.Translate(expression, search), cancellationToken);

    /// <inheritdoc />
    public async Task<T?> GetByCode<T>(string code, CancellationToken cancellationToken = default) where T : DBModel
    {
        var item = await provider.GetByCode<T>(code, cancellationToken);
        if (item is null) return null;
        await extensions.Load(new[] { item }, cancellationToken);
        ApplyDefaults(new[] { item });
        return item;
    }

    /// <inheritdoc />
    public Task<PagedResult<T>> Search<T>(string text, SearchParam? search = null, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null) where T : DBModel
    {
        var param = search?.Clone() ?? new SearchParam();
        param.Search = text;
        return Select<T>(param, skip, limit, cancellationToken, batch);
    }

    /// <inheritdoc />
    public Task<long> Count<T>(SearchParam? search = null, CancellationToken cancellationToken = default) where T : DBModel => provider.Count<T>(searchNormalizer.Normalize<T>(search), cancellationToken);
    /// <inheritdoc />
    public Task<long> Count<T>(Expression<Func<T, bool>> expression, SearchParam? search = null, CancellationToken cancellationToken = default) where T : DBModel => Count<T>(ExpressionTranslator.Translate(expression, search), cancellationToken);

    /// <inheritdoc />
    public async Task<T> Save<T>(T model, CancellationToken cancellationToken = default) where T : DBModel
    {
        var result = await Save(new[] { model }, cancellationToken, 1);
        return result[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> Save<T>(IEnumerable<T> models, CancellationToken cancellationToken = default, int? batch = null) where T : DBModel
    {
        var items = models.ToList();
        if (items.Count == 0) return items;
        Prepare(items);
        var batchSize = BatchResolver.Resolve(batch, options.Batch.Save);
        var modelMetadata = metadata.GetMetadata<T>();
        await transactionManager.Execute(async () =>
        {
            var current = transactionManager.Current!;
            foreach (var group in items.GroupBy(x => x.DataState))
            {
                if (group.Key == DataState.Unchanged) continue;
                foreach (var chunk in group.Chunk(batchSize))
                {
                    IReadOnlyList<T> list = chunk;
                    if (group.Key == DataState.New)
                    {
                        await provider.Insert(list, current, cancellationToken);
                    }
                    else if (group.Key == DataState.Changed) await provider.Update(list, current, cancellationToken);
                    else await provider.Delete(list, modelMetadata.HardDelete, current, cancellationToken);
                    if (group.Key is DataState.New or DataState.Changed) await extensions.Save(list, current, cancellationToken);
                    if (options.AuditTrail.Enabled && modelMetadata.AuditEnabled) await audit.Write(group.Key.ToString(), list, current, cancellationToken);
                }
            }
        }, cancellationToken);

        foreach (var item in items)
        {
            if (item.DataState == DataState.Changed
                || (item.DataState == DataState.Removed && !modelMetadata.HardDelete))
            {
                item.Version++;
            }

            item.DataState = DataState.Unchanged;
        }

        ApplyDefaults(items);
        return items;
    }

    /// <inheritdoc />
    public string GenerateDebugQuery<T>(SearchParam? search = null, int skip = 0, int limit = 100) where T : DBModel => provider.GenerateDebugQuery<T>(searchNormalizer.Normalize<T>(search), skip, limit);

    private void Prepare<T>(IEnumerable<T> items) where T : DBModel
    {
        var now = DateTime.UtcNow;
        var modelMetadata = metadata.GetMetadata<T>();
        var tenantRequired = options.MultiTenancy.Enabled
            && modelMetadata.TenantScoped;
        var tenant = tenantRequired
            ? tenantProvider.GetTenant()
            : null;

        if (tenantRequired && string.IsNullOrWhiteSpace(tenant))
        {
            throw new InvalidOperationException(
                $"Tenant is required for model '{modelMetadata.ModelName}'.");
        }

        foreach (var item in items)
        {
            if (tenantRequired)
            {
                item.Tenant = tenant;
            }
            else if (!modelMetadata.TenantScoped)
            {
                item.Tenant = null;
            }
            if (item.DataState == DataState.New)
            {
                if (string.IsNullOrWhiteSpace(item.Code)) item.Code = item.GenerateCode(modelMetadata.CodeLength, options.CodeGeneration.Separator);
                if (item.Version <= 0) item.Version = 1;
                if (item.CreatedAt == default) item.CreatedAt = now;
                item.CreatedBy ??= userProvider.GetUserCode();
            }
            else
            {
                item.UpdatedAt = now;
                item.UpdatedBy = userProvider.GetUserCode();
                if (item.DataState == DataState.Removed && !modelMetadata.HardDelete) item.DeletedAt = now;
            }
        }
    }

    private void ApplyDefaults<T>(IEnumerable<T> items) where T : DBModel
    {
        var modelMetadata = metadata.GetMetadata<T>();
        foreach (var item in items)
        {
            item.DataState = DataState.Unchanged;

            if (!modelMetadata.TenantScoped)
            {
                item.Tenant = null;
            }

            foreach (var column in modelMetadata.Columns.Where(
                         column => column.DefaultOnReturn))
            {
                column.Property.SetValue(
                    item,
                    column.PropertyType.IsValueType
                        ? Activator.CreateInstance(column.PropertyType)
                        : null);
            }
        }
    }
}
