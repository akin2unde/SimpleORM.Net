using MongoDB.Bson;
using MongoDB.Driver;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// Synchronizes only SimpleORM-managed MongoDB indexes.
/// MongoDB document fields themselves do not require relational-style migrations.
/// </summary>
public sealed class MongoIndexSynchronizer : IDBSchemaSynchronizer
{
    private const string ManagedIndexPrefix = "SORM_";

    private readonly MongoDatabaseProvider _provider;
    private readonly IDBMetadataProvider _metadata;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoIndexSynchronizer"/> class.
    /// </summary>
    public MongoIndexSynchronizer(
        MongoDatabaseProvider provider,
        IDBMetadataProvider metadata,
        SimpleOrmOptions options)
    {
        _provider = provider;
        _metadata = metadata;
        _options = options;
    }

    /// <inheritdoc />
    public async Task Synchronize(
        CancellationToken cancellationToken = default)
    {
        if (!_options.AutoMigration)
        {
            return;
        }

        foreach (var model in _metadata.GetRegisteredModels())
        {
            var collection = _provider.Database.GetCollection<BsonDocument>(
                model.TableName);

            var desiredIndexes = BuildDesiredIndexes(model);
            var existingIndexes = await ReadManagedIndexNames(
                collection,
                cancellationToken);

            foreach (var desired in desiredIndexes)
            {
                if (existingIndexes.Contains(
                        desired.Name,
                        StringComparer.Ordinal))
                {
                    continue;
                }

                var keys = Builders<BsonDocument>.IndexKeys.Combine(
                    desired.Columns.Select(
                        column => Builders<BsonDocument>.IndexKeys.Ascending(column)));

                var index = new CreateIndexModel<BsonDocument>(
                    keys,
                    new CreateIndexOptions
                    {
                        Name = desired.Name,
                        Unique = true
                    });

                await collection.Indexes.CreateOneAsync(
                    index,
                    cancellationToken: cancellationToken);
            }

            foreach (var existing in existingIndexes)
            {
                if (desiredIndexes.Any(
                        desired => desired.Name.Equals(
                            existing,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                await collection.Indexes.DropOneAsync(
                    existing,
                    cancellationToken);
            }
        }
    }

    private static IReadOnlyList<(string Name, IReadOnlyList<string> Columns)> BuildDesiredIndexes(
        DBModelMetadata model)
    {
        var indexes = new List<(string Name, IReadOnlyList<string> Columns)>();

        foreach (var column in model.PersistedColumns
                     .Where(column => column.Unique)
                     .Where(column => string.IsNullOrWhiteSpace(column.UniqueGroup)))
        {
            indexes.Add(
                (
                    $"{ManagedIndexPrefix}UQ_{NormalizeName(model.TableName)}_{NormalizeName(column.ColumnName)}",
                    new[] { column.ColumnName }
                ));
        }

        foreach (var group in model.PersistedColumns
                     .Where(column => column.Unique)
                     .Where(column => !string.IsNullOrWhiteSpace(column.UniqueGroup))
                     .GroupBy(
                         column => column.UniqueGroup!,
                         StringComparer.OrdinalIgnoreCase))
        {
            indexes.Add(
                (
                    $"{ManagedIndexPrefix}UQ_{NormalizeName(model.TableName)}_{NormalizeName(group.Key)}",
                    group.Select(column => column.ColumnName).ToArray()
                ));
        }

        return indexes;
    }

    private static async Task<IReadOnlyList<string>> ReadManagedIndexNames(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(
            cancellationToken);

        var indexes = await cursor.ToListAsync(
            cancellationToken);

        return indexes
            .Where(index => index.Contains("name"))
            .Select(index => index["name"].AsString)
            .Where(name => name.StartsWith(
                ManagedIndexPrefix,
                StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeName(string value)
    {
        return new string(
            value.Select(
                    character => char.IsLetterOrDigit(character) || character == '_'
                        ? character
                        : '_')
                .ToArray());
    }
}
