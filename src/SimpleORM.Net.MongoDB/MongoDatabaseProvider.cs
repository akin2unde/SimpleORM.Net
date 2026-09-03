using System.Dynamic;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.MongoDB;

/// <summary>MongoDB provider.</summary>
public sealed class MongoDatabaseProvider : IDatabaseProvider, IDBQuery
{

    private readonly SimpleOrmOptions _o;

    private readonly IDBMetadataProvider _m;

    private readonly ITenantProvider _t;

    private readonly IMongoClient _client;

    private readonly IMongoDatabase _db;

    /// <summary>Creates provider.</summary>
    public MongoDatabaseProvider(SimpleOrmOptions o, IDBMetadataProvider m, ITenantProvider t)
    {
        _o = o;

        _m = m;

        _t = t;

        var c = o.Connection;

        var cs = string.IsNullOrWhiteSpace(c.Username) ? $"mongodb://{c.Host}:{c.Port}" : $"mongodb://{Uri.EscapeDataString(c.Username)}:{Uri.EscapeDataString(c.Password ?? "")}@{c.Host}:{c.Port}/{c.Database}";
        cs += "?replicaSet=rs0&directConnection=true";
        _client = new MongoClient(cs);

        _db = _client.GetDatabase(c.Database);

    }

    /// <inheritdoc />
    public async Task<IDBTransaction> BeginTransaction(CancellationToken ct = default)
    {
        var s = await _client.StartSessionAsync(cancellationToken: ct);

        s.StartTransaction();

        return new MongoTx(s);

    }

    /// <inheritdoc />
    public async Task<T?> SelectSingle<T>(
        SearchParam search,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        return await Col<T>()
            .Find(Filter<T>(search))
            .Sort(Sort<T>(search))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<T?> GetByCode<T>(string code, CancellationToken ct = default) where T : DBModel
    {
        var p = new SearchParam();

        p.Filters.Add(new SearchFilter
        {
            Field = nameof(DBModel.Code),
            Operator = SearchOperator.EQ,
            Value = code
        }
        );

        return SelectSingle<T>(p, ct);

    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> Select<T>(
        SearchParam search,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = Col<T>()
            .Find(Filter<T>(search))
            .Sort(Sort<T>(search))
            .Skip(skip);

        if (limit > 0)
        {
            query = query.Limit(limit);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<dynamic>> SelectDynamic<T>(
        SearchParam search,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (search.Joins.Count > 0)
        {
            throw new NotSupportedException(
                "MongoDB dynamic selection does not support joins.");
        }

        var metadata = _m.GetMetadata<T>();
        var selectedColumns = search.Fields
            .Select(field => metadata.PersistedColumns.FirstOrDefault(
                column => column.PropertyName.Equals(
                    field,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Property '{field}' is not a persisted field on model '{metadata.ModelName}'."))
            .DistinctBy(
                column => column.PropertyName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedColumns.Count == 0)
        {
            throw new InvalidOperationException(
                "SelectDynamic requires at least one selected field.");
        }

        var projectionParts = selectedColumns
            .Select(column => Builders<T>.Projection.Include(column.ColumnName))
            .ToList();

        projectionParts.Add(
            Builders<T>.Projection.Exclude("_id"));

        var query = Col<T>()
            .Find(Filter<T>(search))
            .Sort(Sort<T>(search))
            .Skip(skip);

        if (limit > 0)
        {
            query = query.Limit(limit);
        }

        var documents = await query
            .Project<BsonDocument>(
                Builders<T>.Projection.Combine(projectionParts))
            .ToListAsync(cancellationToken);

        var rows = new List<dynamic>(documents.Count);

        foreach (var document in documents)
        {
            IDictionary<string, object?> row = new ExpandoObject();

            foreach (var column in selectedColumns)
            {
                row[column.PropertyName] = document.TryGetValue(
                    column.ColumnName,
                    out var value)
                    && !value.IsBsonNull
                        ? BsonTypeMapper.MapToDotNetValue(value)
                        : null;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <inheritdoc />
    public Task<long> Count<T>(SearchParam p, CancellationToken ct = default) where T : DBModel => Col<T>().CountDocumentsAsync(Filter<T>(p), cancellationToken: ct);

    /// <inheritdoc />
    public async Task Insert<T>(IReadOnlyList<T> x, IDBTransaction tr, CancellationToken ct = default) where T : DBModel
    {
        if (x.Count == 0)
        {
            return;
        }

        var metadata = _m.GetMetadata<T>();
        var documents = ToPersistedDocuments(x);

        var collection = _db.GetCollection<BsonDocument>(
            metadata.TableName);

        await collection.InsertManyAsync(
            As(tr).Session,
            documents,
            cancellationToken: ct);

    }

    /// <inheritdoc />
    // public async Task Update<T>(IReadOnlyList<T> x, IDBTransaction tr, CancellationToken ct = default) where T : DBModel
    // {
    //     var w = x.Select(v => new ReplaceOneModel<T>(Scoped<T>(Builders<T>.Filter.Eq(y => y.Code, v.Code), false), v)).Cast<WriteModel<T>>().ToArray();

    //     if (w.Length > 0) await Col<T>().BulkWriteAsync(As(tr).Session, w, cancellationToken: ct);

    // }
    public async Task Update<T>(
    IReadOnlyList<T> models,
    IDBTransaction transaction,
    CancellationToken cancellationToken = default)
    where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(models);

        if (models.Count == 0)
        {
            return;
        }

        var metadata = _m.GetMetadata<T>();

        var collection = _db.GetCollection<BsonDocument>(
            metadata.TableName);

        var writes = models
            .Select(model =>
            {
                var document = ToPersistedDocument(
                    model,
                    metadata);

                var filter = CreateUpdateFilter(
                    model,
                    metadata);

                return new ReplaceOneModel<BsonDocument>(
                    filter,
                    document)
                {
                    IsUpsert = model.Upsert
                };
            })
            .Cast<WriteModel<BsonDocument>>()
            .ToArray();

        await collection.BulkWriteAsync(
            As(transaction).Session,
            writes,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task Delete<T>(IReadOnlyList<T> x, bool hard, IDBTransaction tr, CancellationToken ct = default) where T : DBModel
    {
        if (hard) await Col<T>().DeleteManyAsync(As(tr).Session, Scoped<T>(Builders<T>.Filter.In(y => y.Code, x.Select(y => y.Code)), false), cancellationToken: ct);

        else
        {
            var w = x.Select(v => new UpdateOneModel<T>(Scoped<T>(Builders<T>.Filter.Eq(y => y.Code, v.Code), false), Builders<T>.Update.Set(y => y.DeletedAt, v.DeletedAt ?? DateTime.UtcNow).Set(y => y.UpdatedAt, v.UpdatedAt).Set(y => y.UpdatedBy, v.UpdatedBy))).Cast<WriteModel<T>>().ToArray();

            if (w.Length > 0) await Col<T>().BulkWriteAsync(As(tr).Session, w, cancellationToken: ct);

        }
    }

    /// <inheritdoc />
    public async Task<long> DeleteStale(
        DBModelMetadata metadata,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var createdAtColumn = metadata.PersistedColumns.FirstOrDefault(
            column => column.PropertyName == nameof(DBModel.CreatedAt))
            ?? throw new InvalidOperationException(
                $"Model '{metadata.ModelName}' does not persist CreatedAt and cannot use stale-data cleanup.");

        var collection = _db.GetCollection<BsonDocument>(
            metadata.TableName);
        var filter = Builders<BsonDocument>.Filter.Lt(
            createdAtColumn.ColumnName,
            olderThanUtc);
        var result = await collection.DeleteManyAsync(
            filter,
            cancellationToken);

        return result.DeletedCount;
    }

    /// <inheritdoc />
    public string GenerateDebugQuery<T>(SearchParam p, int skip = 0, int limit = 100) where T : DBModel => JsonSerializer.Serialize(new
    {
        collection = _m.GetMetadata<T>().TableName,
        skip,
        limit,
        note = "MongoDB filter is generated by provider at execution time."
    }
    , new JsonSerializerOptions
    {
        WriteIndented = true
    }
    );

    /// <inheritdoc />
    public async Task<dynamic?> QuerySingle(string q, object? p = null, CancellationToken ct = default) => (await Query(q, p, ct)).FirstOrDefault();

    /// <inheritdoc />
    public async Task<IReadOnlyList<dynamic>> Query(string q, object? p = null, CancellationToken ct = default)
    {
        var d = BsonDocument.Parse(q);

        var c = _db.GetCollection<BsonDocument>(d["collection"].AsString);

        var f = d.TryGetValue("filter", out var v) ? v.AsBsonDocument : new BsonDocument();

        var r = await c.Find(f).ToListAsync(ct);

        return r.Select(x => (dynamic)JsonSerializer.Deserialize<ExpandoObject>(x.ToJson())!).ToArray();

    }

    /// <inheritdoc />
    public Task<T?> QuerySingle<T>(string q, object? p = null, CancellationToken ct = default) => throw new NotSupportedException("Use dynamic raw Mongo query in MVP or IDataRepository.");

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> Query<T>(string q, object? p = null, CancellationToken ct = default) => throw new NotSupportedException("Use dynamic raw Mongo query in MVP or IDataRepository.");

    /// <inheritdoc />
    public Task<long> Execute(string q, object? p = null, CancellationToken ct = default) => throw new NotSupportedException("Mongo raw Execute is not generalized in MVP.");

    internal IMongoDatabase Database => _db;
    private IReadOnlyList<BsonDocument> ToPersistedDocuments<T>(
        IReadOnlyList<T> models)
        where T : DBModel
    {
        var metadata = _m.GetMetadata<T>();

        var persistedNames = metadata.PersistedColumns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return models
            .Select(model =>
            {
                var serialized = model.ToBsonDocument();

                return new BsonDocument(
                    serialized.Elements.Where(element =>
                        persistedNames.Contains(element.Name)));
            })
            .ToList();
    }
    private FilterDefinition<BsonDocument> CreateUpdateFilter<T>(
    T model,
    DBModelMetadata metadata)
    where T : DBModel
    {
        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        var codeColumn = metadata.Columns.First(
            column => string.Equals(
                column.PropertyName,
                nameof(DBModel.Code),
                StringComparison.OrdinalIgnoreCase));

        filters.Add(
            builder.Eq(
                codeColumn.ColumnName,
                model.Code));

        if (_o.MultiTenancy.Enabled
            && metadata.TenantScoped)
        {
            var tenant = _t.GetTenant()
                ?? throw new InvalidOperationException(
                    "Tenant is required.");

            var tenantColumn = metadata.Columns.First(
                column => string.Equals(
                    column.PropertyName,
                    nameof(DBModel.Tenant),
                    StringComparison.OrdinalIgnoreCase));

            filters.Add(
                builder.Eq(
                    tenantColumn.ColumnName,
                    tenant));
        }

        if (!metadata.HardDelete)
        {
            var deletedAtColumn = metadata.Columns.First(
                column => string.Equals(
                    column.PropertyName,
                    nameof(DBModel.DeletedAt),
                    StringComparison.OrdinalIgnoreCase));

            filters.Add(
                builder.Eq(
                    deletedAtColumn.ColumnName,
                    BsonNull.Value));
        }

        return builder.And(filters);
    }
    private static BsonDocument ToPersistedDocument<T>(
    T model,
    DBModelMetadata metadata)
    where T : DBModel
    {
        var serializedDocument = model.ToBsonDocument();

        var persistedColumnNames = metadata.PersistedColumns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new BsonDocument(
            serializedDocument.Elements.Where(element =>
                persistedColumnNames.Contains(element.Name)));
    }
    private IMongoCollection<T> Col<T>()
          where T : DBModel
    {

        var metadata = _m.GetMetadata<T>();

        return _db.GetCollection<T>(
            metadata.TableName);
    }
    private FilterDefinition<T> Filter<T>(SearchParam p) where T : DBModel
    {
        var b = Builders<T>.Filter;

        var fs = new List<FilterDefinition<T>>();

        var m = _m.GetMetadata<T>();

        foreach (var x in p.Filters)
        {
            var c = m.PersistedColumns.FirstOrDefault(
                column => column.PropertyName.Equals(
                    x.Field,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Property '{x.Field}' is not a persisted column on model '{m.ModelName}'.");

            fs.Add(x.Operator switch
            {
                SearchOperator.EQ => b.Eq(c.ColumnName, x.Value),
                SearchOperator.NEQ => b.Ne(c.ColumnName, x.Value),
                SearchOperator.Contains => b.Regex(c.ColumnName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(x.Value?.ToString() ?? ""), "i")),
                SearchOperator.In => b.In(c.ColumnName, ((System.Collections.IEnumerable)x.Value!).Cast<object?>()),
                _ => throw new NotSupportedException($"Operator {x.Operator} not implemented in Mongo MVP.")
            }
            );

        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var ors = m.SearchableColumns.Select(c => (FilterDefinition<T>)b.Regex(c.ColumnName, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(p.Search), "i"))).ToArray();

            if (ors.Length > 0) fs.Add(b.Or(ors));

        }
        var basef = fs.Count == 0 ? b.Empty : (p.Condition == SearchCondition.Or ? b.Or(fs) : b.And(fs));

        return Scoped<T>(basef, p.IncludeDeleted);

    }
    private string? GetTenantValueFromFilter<T>(FilterDefinition<T> filter) where T : DBModel
    {
        if (_o.MultiTenancy.Enabled)
        {
            var renderArgs = new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);
            BsonDocument renderedDoc = filter.Render(renderArgs);
            if (renderedDoc.Elements.Any(_ => _.Name == _o.MultiTenancy.JwtClaim))
            {
                var value = renderedDoc.Elements.First(_ => _.Name == _o.MultiTenancy.JwtClaim).Value;
                if (value is null) return null;
                return value.AsString;
            }
        }
        return null;
    }
    private FilterDefinition<T> Scoped<T>(FilterDefinition<T> f, bool includeDeleted) where T : DBModel
    {
        var b = Builders<T>.Filter;

        var all = new List<FilterDefinition<T>>
        {
            f
        }
        ;

        var metadata = _m.GetMetadata<T>();

        if (_o.MultiTenancy.Enabled
            && metadata.TenantScoped)
        {
            all.Add(
                b.Eq(
                    x => x.Tenant,
                    _t.GetTenant() ?? GetTenantValueFromFilter<T>(f)
                    ?? throw new InvalidOperationException(
                        $"Tenant is required for model '{metadata.ModelName}'.")));
        }

        if (!metadata.HardDelete && !includeDeleted)
        {
            all.Add(b.Eq(x => x.DeletedAt, null));
        }

        return b.And(all);

    }

    private SortDefinition<T> Sort<T>(SearchParam p) where T : DBModel
    {
        var b = Builders<T>.Sort;

        var m = _m.GetMetadata<T>();

        var x = p.OrderBy
            .Select(order =>
            {
                var column = m.PersistedColumns.FirstOrDefault(
                    item => item.PropertyName.Equals(
                        order.Field,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Property '{order.Field}' is not a persisted column on model '{m.ModelName}'.");

                return order.Descending
                    ? b.Descending(column.ColumnName)
                    : b.Ascending(column.ColumnName);
            })
            .ToList();

        x.Add(b.Ascending(nameof(DBModel.Code)));

        return b.Combine(x);

    }

    private static MongoTx As(IDBTransaction t) => t as MongoTx ?? throw new InvalidOperationException("Wrong provider transaction.");

}
