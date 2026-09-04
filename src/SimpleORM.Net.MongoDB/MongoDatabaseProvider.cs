using System.Dynamic;

using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;
using SimpleORM.Net.Exceptions;

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

        var metadata = _m.GetMetadata<T>();
        var pipeline = new List<BsonDocument>
        {
            new("$match", BuildDocumentFilter(metadata, search))
        };

        foreach (var join in search.Joins)
        {
            if (join.Type is JoinType.RightOuter or JoinType.FullOuter)
            {
                throw new NotSupportedException(
                    $"MongoDB SelectDynamic currently supports Inner and LeftOuter joins; '{join.Type}' is not supported.");
            }

            var joinMetadata = _m.GetMetadata(join.Model);
            var localColumn = FindPersistedColumn(metadata, join.LocalField);
            var foreignColumn = FindPersistedColumn(joinMetadata, join.ForeignField);
            var alias = string.IsNullOrWhiteSpace(join.Alias)
                ? join.Model.Name
                : join.Alias.Trim();

            pipeline.Add(
                new BsonDocument(
                    "$lookup",
                    new BsonDocument
                    {
                        { "from", joinMetadata.TableName },
                        { "localField", localColumn.ColumnName },
                        { "foreignField", foreignColumn.ColumnName },
                        { "as", alias }
                    }));

            pipeline.Add(
                new BsonDocument(
                    "$unwind",
                    new BsonDocument
                    {
                        { "path", "$" + alias },
                        { "preserveNullAndEmptyArrays", join.Type == JoinType.LeftOuter }
                    }));

            var joinedScope = BuildJoinedDocumentScope(
                joinMetadata,
                alias,
                join.Type);

            if (joinedScope.ElementCount > 0)
            {
                pipeline.Add(new BsonDocument("$match", joinedScope));
            }
        }

        var sort = BuildDocumentSort(metadata, search);
        if (sort.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$sort", sort));
        }

        if (skip > 0)
        {
            pipeline.Add(new BsonDocument("$skip", skip));
        }

        if (limit > 0)
        {
            pipeline.Add(new BsonDocument("$limit", limit));
        }

        var projection = new BsonDocument { { "_id", 0 } };

        foreach (var field in search.Fields)
        {
            var column = FindPersistedColumn(metadata, field);
            projection[column.PropertyName] = "$" + column.ColumnName;
        }

        foreach (var join in search.Joins)
        {
            var joinMetadata = _m.GetMetadata(join.Model);
            var alias = string.IsNullOrWhiteSpace(join.Alias)
                ? join.Model.Name
                : join.Alias.Trim();

            foreach (var field in join.Fields)
            {
                var column = FindPersistedColumn(joinMetadata, field);
                projection[$"{alias}_{column.PropertyName}"] =
                    $"${alias}.{column.ColumnName}";
            }
        }

        if (projection.ElementCount == 1)
        {
            throw new InvalidOperationException(
                "SelectDynamic requires at least one selected field.");
        }

        pipeline.Add(new BsonDocument("$project", projection));

        var collection = _db.GetCollection<BsonDocument>(metadata.TableName);
        var documents = await collection.Aggregate<BsonDocument>(pipeline)
            .ToListAsync(cancellationToken);

        return documents
            .Select(ToDynamic)
            .ToArray();
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

        if (metadata.ConcurrencyEnabled && models.Any(model => model.Upsert))
        {
            throw new InvalidOperationException(
                $"Upsert cannot be used for concurrency-protected model '{metadata.ModelName}'. " +
                "Save new records with DataState.New, or decorate the model with [DisableConcurrencyCheck] when last-write-wins upsert behavior is intentional.");
        }

        var collection = _db.GetCollection<BsonDocument>(
            metadata.TableName);
        var versionColumn = metadata.PersistedColumns.First(
            column => column.PropertyName == nameof(DBModel.Version));

        var writes = models
            .Select(model =>
            {
                var document = ToPersistedDocument(
                    model,
                    metadata);
                document[versionColumn.ColumnName] = model.Version + 1;

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

        var result = await collection.BulkWriteAsync(
            As(transaction).Session,
            writes,
            cancellationToken: cancellationToken);

        if (metadata.ConcurrencyEnabled && result.MatchedCount != models.Count)
        {
            throw new DBConcurrencyException(
                typeof(T),
                models.Select(model => model.Code));
        }
    }

    /// <inheritdoc />
    public async Task Delete<T>(
        IReadOnlyList<T> models,
        bool hardDelete,
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
        var collection = _db.GetCollection<BsonDocument>(metadata.TableName);
        var versionColumn = metadata.PersistedColumns.First(
            column => column.PropertyName == nameof(DBModel.Version));
        BulkWriteResult<BsonDocument> result;

        if (hardDelete)
        {
            var writes = models
                .Select(model => (WriteModel<BsonDocument>)new DeleteOneModel<BsonDocument>(
                    CreateUpdateFilter(model, metadata)))
                .ToArray();

            result = await collection.BulkWriteAsync(
                As(transaction).Session,
                writes,
                cancellationToken: cancellationToken);

            if (metadata.ConcurrencyEnabled && result.DeletedCount != models.Count)
            {
                throw new DBConcurrencyException(
                    typeof(T),
                    models.Select(model => model.Code));
            }

            return;
        }

        var deletedAtColumn = metadata.PersistedColumns.First(
            column => column.PropertyName == nameof(DBModel.DeletedAt));
        var updatedAtColumn = metadata.PersistedColumns.First(
            column => column.PropertyName == nameof(DBModel.UpdatedAt));
        var updatedByColumn = metadata.PersistedColumns.First(
            column => column.PropertyName == nameof(DBModel.UpdatedBy));
        var softDeleteWrites = models
            .Select(model =>
            {
                var update = Builders<BsonDocument>.Update
                    .Set(deletedAtColumn.ColumnName, model.DeletedAt ?? DateTime.UtcNow)
                    .Set(updatedAtColumn.ColumnName, model.UpdatedAt)
                    .Set(updatedByColumn.ColumnName, model.UpdatedBy)
                    .Inc(versionColumn.ColumnName, 1);

                return (WriteModel<BsonDocument>)new UpdateOneModel<BsonDocument>(
                    CreateUpdateFilter(model, metadata),
                    update);
            })
            .ToArray();

        result = await collection.BulkWriteAsync(
            As(transaction).Session,
            softDeleteWrites,
            cancellationToken: cancellationToken);

        if (metadata.ConcurrencyEnabled && result.MatchedCount != models.Count)
        {
            throw new DBConcurrencyException(
                typeof(T),
                models.Select(model => model.Code));
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
    public async Task<T?> QuerySingle<T>(
        string q,
        object? p = null,
        CancellationToken ct = default)
    {
        var rows = await Query<T>(q, p, ct);
        return rows.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> Query<T>(
        string q,
        object? p = null,
        CancellationToken ct = default)
    {
        var request = ParseRawRequest(q);
        var collection = _db.GetCollection<BsonDocument>(request.Collection);
        var documents = await collection
            .Find(request.Filter)
            .ToListAsync(ct);

        return documents
            .Select(ConvertDocument<T>)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<long> Execute(
        string q,
        object? p = null,
        CancellationToken ct = default)
    {
        var document = BsonDocument.Parse(q);
        var collectionName = document.GetValue("collection", "").AsString;

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException(
                "Mongo raw Execute requires a 'collection' property.",
                nameof(q));
        }

        var operation = document.GetValue("operation", "noop").AsString;
        var collection = _db.GetCollection<BsonDocument>(collectionName);
        var filter = document.TryGetValue("filter", out var filterValue)
            ? filterValue.AsBsonDocument
            : new BsonDocument();

        switch (operation.ToLowerInvariant())
        {
            case "noop":
                return 0;

            case "deletemany":
            case "delete":
            {
                var result = await collection.DeleteManyAsync(filter, ct);
                return result.DeletedCount;
            }

            case "updatemany":
            case "update":
            {
                if (!document.TryGetValue("update", out var updateValue)
                    || !updateValue.IsBsonDocument)
                {
                    throw new ArgumentException(
                        "Mongo raw update requires an 'update' document.",
                        nameof(q));
                }

                var result = await collection.UpdateManyAsync(
                    filter,
                    updateValue.AsBsonDocument,
                    cancellationToken: ct);
                return result.ModifiedCount;
            }

            case "insertone":
            case "insert":
            {
                if (!document.TryGetValue("document", out var insertValue)
                    || !insertValue.IsBsonDocument)
                {
                    throw new ArgumentException(
                        "Mongo raw insert requires a 'document' object.",
                        nameof(q));
                }

                await collection.InsertOneAsync(
                    insertValue.AsBsonDocument,
                    cancellationToken: ct);
                return 1;
            }

            default:
                throw new NotSupportedException(
                    $"Mongo raw operation '{operation}' is not supported. " +
                    "Supported operations are noop, insertOne, updateMany and deleteMany.");
        }
    }

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

        if (metadata.ConcurrencyEnabled)
        {
            var versionColumn = metadata.PersistedColumns.First(
                column => string.Equals(
                    column.PropertyName,
                    nameof(DBModel.Version),
                    StringComparison.OrdinalIgnoreCase));

            var versionFilter = builder.Eq(
                versionColumn.ColumnName,
                model.Version);

            // Backward compatibility for Mongo documents created before Version existed.
            if (model.Version == 1)
            {
                versionFilter = builder.Or(
                    versionFilter,
                    builder.Exists(versionColumn.ColumnName, false));
            }

            filters.Add(versionFilter);
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

            fs.Add(BuildTypedFilter(b, c, x));

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
    private static FilterDefinition<T> BuildTypedFilter<T>(
        FilterDefinitionBuilder<T> builder,
        DBColumnMetadata column,
        SearchFilter filter)
        where T : DBModel
    {
        var field = column.ColumnName;

        return filter.Operator switch
        {
            SearchOperator.EQ => builder.Eq(field, filter.Value),
            SearchOperator.NEQ => builder.Ne(field, filter.Value),
            SearchOperator.GT => builder.Gt(field, filter.Value),
            SearchOperator.GTE => builder.Gte(field, filter.Value),
            SearchOperator.LT => builder.Lt(field, filter.Value),
            SearchOperator.LTE => builder.Lte(field, filter.Value),
            SearchOperator.Contains => builder.Regex(
                field,
                RegexValue(filter.Value, prefix: false, suffix: false)),
            SearchOperator.StartsWith => builder.Regex(
                field,
                RegexValue(filter.Value, prefix: true, suffix: false)),
            SearchOperator.EndsWith => builder.Regex(
                field,
                RegexValue(filter.Value, prefix: false, suffix: true)),
            SearchOperator.In => builder.In(
                field,
                GetEnumerableValues(filter.Value, nameof(SearchOperator.In))),
            SearchOperator.NotIn => builder.Nin(
                field,
                GetEnumerableValues(filter.Value, nameof(SearchOperator.NotIn))),
            SearchOperator.IsNull => builder.Or(
                builder.Eq(field, BsonNull.Value),
                builder.Exists(field, false)),
            SearchOperator.IsNotNull => builder.And(
                builder.Exists(field, true),
                builder.Ne(field, BsonNull.Value)),
            SearchOperator.Between => BuildTypedRange(
                builder,
                field,
                filter.Value,
                negate: false),
            SearchOperator.NotBetween => BuildTypedRange(
                builder,
                field,
                filter.Value,
                negate: true),
            _ => throw new NotSupportedException(
                $"Search operator '{filter.Operator}' is not supported by MongoDB.")
        };
    }

    private static FilterDefinition<T> BuildTypedRange<T>(
        FilterDefinitionBuilder<T> builder,
        string field,
        object? value,
        bool negate)
        where T : DBModel
    {
        var values = GetEnumerableValues(
                value,
                negate ? nameof(SearchOperator.NotBetween) : nameof(SearchOperator.Between))
            .Take(3)
            .ToArray();

        if (values.Length != 2)
        {
            throw new ArgumentException(
                $"{(negate ? "NotBetween" : "Between")} requires exactly two values.");
        }

        return negate
            ? builder.Or(
                builder.Lt(field, values[0]),
                builder.Gt(field, values[1]))
            : builder.And(
                builder.Gte(field, values[0]),
                builder.Lte(field, values[1]));
    }

    private static IEnumerable<object?> GetEnumerableValues(
        object? value,
        string operation)
    {
        if (value is string || value is not System.Collections.IEnumerable enumerable)
        {
            throw new ArgumentException(
                $"{operation} requires an enumerable value.");
        }

        return enumerable.Cast<object?>();
    }

    private static BsonRegularExpression RegexValue(
        object? value,
        bool prefix,
        bool suffix)
    {
        var text = Regex.Escape(value?.ToString() ?? string.Empty);
        var pattern = (prefix ? "^" : string.Empty)
                      + text
                      + (suffix ? "$" : string.Empty);
        return new BsonRegularExpression(pattern, "i");
    }

    private BsonDocument BuildDocumentFilter(
        DBModelMetadata metadata,
        SearchParam search)
    {
        var userFilters = search.Filters
            .Select(filter => BuildDocumentSearchFilter(metadata, filter))
            .ToList();

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var searchFilters = metadata.SearchableColumns
                .Select(column => new BsonDocument(
                    column.ColumnName,
                    new BsonRegularExpression(
                        Regex.Escape(search.Search),
                        "i")))
                .Cast<BsonValue>()
                .ToArray();

            if (searchFilters.Length > 0)
            {
                userFilters.Add(
                    new BsonDocument(
                        "$or",
                        new BsonArray(searchFilters)));
            }
        }

        var scoped = new List<BsonDocument>();

        if (userFilters.Count == 1)
        {
            scoped.Add(userFilters[0]);
        }
        else if (userFilters.Count > 1)
        {
            scoped.Add(
                new BsonDocument(
                    search.Condition == SearchCondition.Or ? "$or" : "$and",
                    new BsonArray(userFilters)));
        }

        if (_o.MultiTenancy.Enabled && metadata.TenantScoped)
        {
            var tenantColumn = metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Model '{metadata.ModelName}' has no tenant column.");
            var tenant = _t.GetTenant()
                ?? throw new InvalidOperationException(
                    $"Tenant is required for model '{metadata.ModelName}'.");
            scoped.Add(new BsonDocument(tenantColumn.ColumnName, tenant));
        }

        if (!metadata.HardDelete && !search.IncludeDeleted)
        {
            var deletedAt = FindPersistedColumn(metadata, nameof(DBModel.DeletedAt));
            scoped.Add(new BsonDocument(deletedAt.ColumnName, BsonNull.Value));
        }

        return scoped.Count switch
        {
            0 => new BsonDocument(),
            1 => scoped[0],
            _ => new BsonDocument("$and", new BsonArray(scoped))
        };
    }

    private static BsonDocument BuildDocumentSearchFilter(
        DBModelMetadata metadata,
        SearchFilter filter)
    {
        var column = FindPersistedColumn(metadata, filter.Field);
        var field = column.ColumnName;
        var value = ToBsonValue(filter.Value);

        return filter.Operator switch
        {
            SearchOperator.EQ => new BsonDocument(field, value),
            SearchOperator.NEQ => new BsonDocument(field, new BsonDocument("$ne", value)),
            SearchOperator.GT => new BsonDocument(field, new BsonDocument("$gt", value)),
            SearchOperator.GTE => new BsonDocument(field, new BsonDocument("$gte", value)),
            SearchOperator.LT => new BsonDocument(field, new BsonDocument("$lt", value)),
            SearchOperator.LTE => new BsonDocument(field, new BsonDocument("$lte", value)),
            SearchOperator.Contains => new BsonDocument(field, RegexValue(filter.Value, false, false)),
            SearchOperator.StartsWith => new BsonDocument(field, RegexValue(filter.Value, true, false)),
            SearchOperator.EndsWith => new BsonDocument(field, RegexValue(filter.Value, false, true)),
            SearchOperator.In => new BsonDocument(field, new BsonDocument(
                "$in",
                new BsonArray(GetEnumerableValues(filter.Value, "In").Select(ToBsonValue)))),
            SearchOperator.NotIn => new BsonDocument(field, new BsonDocument(
                "$nin",
                new BsonArray(GetEnumerableValues(filter.Value, "NotIn").Select(ToBsonValue)))),
            SearchOperator.IsNull => new BsonDocument(
                "$or",
                new BsonArray
                {
                    new BsonDocument(field, BsonNull.Value),
                    new BsonDocument(field, new BsonDocument("$exists", false))
                }),
            SearchOperator.IsNotNull => new BsonDocument(
                "$and",
                new BsonArray
                {
                    new BsonDocument(field, new BsonDocument("$exists", true)),
                    new BsonDocument(field, new BsonDocument("$ne", BsonNull.Value))
                }),
            SearchOperator.Between => BuildDocumentRange(field, filter.Value, false),
            SearchOperator.NotBetween => BuildDocumentRange(field, filter.Value, true),
            _ => throw new NotSupportedException(
                $"Search operator '{filter.Operator}' is not supported by MongoDB.")
        };
    }

    private static BsonDocument BuildDocumentRange(
        string field,
        object? value,
        bool negate)
    {
        var values = GetEnumerableValues(value, negate ? "NotBetween" : "Between")
            .Take(3)
            .Select(ToBsonValue)
            .ToArray();

        if (values.Length != 2)
        {
            throw new ArgumentException(
                $"{(negate ? "NotBetween" : "Between")} requires exactly two values.");
        }

        return negate
            ? new BsonDocument(
                "$or",
                new BsonArray
                {
                    new BsonDocument(field, new BsonDocument("$lt", values[0])),
                    new BsonDocument(field, new BsonDocument("$gt", values[1]))
                })
            : new BsonDocument(
                field,
                new BsonDocument
                {
                    { "$gte", values[0] },
                    { "$lte", values[1] }
                });
    }

    private static BsonValue ToBsonValue(object? value)
    {
        if (value is null)
        {
            return BsonNull.Value;
        }

        if (value is Enum enumValue)
        {
            return new BsonString(enumValue.ToString());
        }

        return BsonValue.Create(value);
    }

    private static DBColumnMetadata FindPersistedColumn(
        DBModelMetadata metadata,
        string propertyName)
    {
        return metadata.PersistedColumns.FirstOrDefault(column =>
                   column.PropertyName.Equals(
                       propertyName,
                       StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"Property '{propertyName}' is not a persisted column on model '{metadata.ModelName}'.");
    }

    private BsonDocument BuildJoinedDocumentScope(
        DBModelMetadata metadata,
        string alias,
        JoinType joinType)
    {
        var conditions = new List<BsonDocument>();

        if (_o.MultiTenancy.Enabled && metadata.TenantScoped)
        {
            var tenantColumn = metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Model '{metadata.ModelName}' has no tenant column.");
            var tenant = _t.GetTenant()
                ?? throw new InvalidOperationException(
                    $"Tenant is required for joined model '{metadata.ModelName}'.");

            conditions.Add(
                new BsonDocument(
                    $"{alias}.{tenantColumn.ColumnName}",
                    tenant));
        }

        if (!metadata.HardDelete)
        {
            var deletedAt = FindPersistedColumn(
                metadata,
                nameof(DBModel.DeletedAt));
            conditions.Add(
                new BsonDocument(
                    $"{alias}.{deletedAt.ColumnName}",
                    BsonNull.Value));
        }

        if (conditions.Count == 0)
        {
            return new BsonDocument();
        }

        var scoped = conditions.Count == 1
            ? conditions[0]
            : new BsonDocument("$and", new BsonArray(conditions));

        if (joinType != JoinType.LeftOuter)
        {
            return scoped;
        }

        return new BsonDocument(
            "$or",
            new BsonArray
            {
                new BsonDocument(alias, new BsonDocument("$exists", false)),
                scoped
            });
    }

    private static BsonDocument BuildDocumentSort(
        DBModelMetadata metadata,
        SearchParam search)
    {
        var sort = new BsonDocument();

        foreach (var order in search.OrderBy)
        {
            var column = FindPersistedColumn(metadata, order.Field);
            sort[column.ColumnName] = order.Descending ? -1 : 1;
        }

        if (!search.OrderBy.Any(order =>
                order.Field.Equals(
                    nameof(DBModel.Code),
                    StringComparison.OrdinalIgnoreCase)))
        {
            sort[metadata.CodeColumn.ColumnName] = 1;
        }

        return sort;
    }

    private static dynamic ToDynamic(BsonDocument document)
    {
        IDictionary<string, object?> row = new ExpandoObject();

        foreach (var element in document.Elements)
        {
            row[element.Name] = element.Value.IsBsonNull
                ? null
                : BsonTypeMapper.MapToDotNetValue(element.Value);
        }

        return (ExpandoObject)row;
    }

    private static (string Collection, BsonDocument Filter) ParseRawRequest(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var request = BsonDocument.Parse(query);
        var collection = request.GetValue("collection", "").AsString;

        if (string.IsNullOrWhiteSpace(collection))
        {
            throw new ArgumentException(
                "Mongo raw query requires a 'collection' property.",
                nameof(query));
        }

        var filter = request.TryGetValue("filter", out var value)
            ? value.AsBsonDocument
            : new BsonDocument();

        return (collection, filter);
    }

    private static T ConvertDocument<T>(BsonDocument document)
    {
        var propertyNames = typeof(T)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var values = document.Elements
            .Where(element => propertyNames.Contains(element.Name))
            .ToDictionary(
                element => element.Name,
                element => element.Value.IsBsonNull
                    ? null
                    : BsonTypeMapper.MapToDotNetValue(element.Value),
                StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(values);
        var result = JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result is null)
        {
            throw new InvalidOperationException(
                $"MongoDB raw query could not materialize '{typeof(T).FullName}'.");
        }

        return result;
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
