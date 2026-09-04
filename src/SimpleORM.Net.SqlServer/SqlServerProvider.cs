using System.Data;
using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Exceptions;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IDatabaseProvider"/> and
/// <see cref="IDBQuery"/>.
/// </summary>
public sealed class SqlServerProvider : IDatabaseProvider, IDBQuery
{
    private readonly SimpleOrmOptions _options;
    private readonly IDBMetadataProvider _metadata;
    private readonly ITenantProvider _tenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerProvider"/> class.
    /// </summary>
    /// <param name="options">SimpleORM configuration.</param>
    /// <param name="metadata">Cached model metadata provider.</param>
    /// <param name="tenant">Current tenant resolver.</param>
    public SqlServerProvider(
        SimpleOrmOptions options,
        IDBMetadataProvider metadata,
        ITenantProvider tenant)
    {
        _options = options;
        _metadata = metadata;
        _tenant = tenant;
    }

    /// <inheritdoc />
    public async Task<IDBTransaction> BeginTransaction(
        CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        try
        {
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

            return new SqlTx(
                connection,
                transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> SelectSingle<T>(
        SearchParam search,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = BuildSelectQuery<T>(
            search,
            skip: 0,
            limit: 1,
            single: true);

        var result = await ReadModels<T>(
            query,
            cancellationToken);

        return result.FirstOrDefault();
    }

    /// <inheritdoc />
    public Task<T?> GetByCode<T>(
        string code,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var search = new SearchParam();

        search.Filters.Add(
            new SearchFilter
            {
                Field = nameof(DBModel.Code),
                Operator = SearchOperator.EQ,
                Value = code
            });

        return SelectSingle<T>(
            search,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<T>> Select<T>(
        SearchParam search,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skip),
                "Skip cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "Limit cannot be negative.");
        }

        var query = BuildSelectQuery<T>(
            search,
            skip,
            limit,
            single: false);

        return ReadModels<T>(
            query,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<dynamic>> SelectDynamic<T>(
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

        var query = BuildSelectQuery<T>(
            search,
            skip,
            limit,
            single: false,
            dynamicProjection: true);

        return ReadDynamic(
            query,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> Count<T>(
        SearchParam search,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        var model = _metadata.GetMetadata<T>();
        var parameters = new Dictionary<string, object?>();

        var from = BuildFromClause(
            model,
            search);

        var where = BuildWhereClause(
            model,
            search,
            parameters);

        var sql = search.Joins.Count == 0
            ? $"""
               SELECT COUNT_BIG(*)
               FROM [{EscapeIdentifier(model.TableName)}] AS [t]
               {where}
               """
            : $"""
               SELECT COUNT_BIG(*)
               FROM
               (
                   SELECT DISTINCT [t].[{EscapeIdentifier(model.CodeColumn.ColumnName)}]
                   FROM [{EscapeIdentifier(model.TableName)}] AS [t]
                   {from}
                   {where}
               ) AS [count_source]
               """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        AddParameters(
            command,
            parameters);

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        if (result is null || result == DBNull.Value)
        {
            return 0L;
        }

        return Convert.ToInt64(
            result,
            CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task Insert<T>(
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(transaction);

        if (models.Count == 0)
        {
            return;
        }

        var sqlTransaction = RequireTransaction(transaction);
        var metadata = _metadata.GetMetadata<T>();
        var columns = metadata.PersistedColumns.ToArray();

        if (columns.Length == 0)
        {
            throw new InvalidOperationException(
                $"Model '{metadata.ModelName}' has no persisted columns.");
        }

        var table = BuildBulkTable(models, columns);

        await BulkCopy(
            sqlTransaction,
            metadata.TableName,
            table,
            columns,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task Update<T>(
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(transaction);

        if (models.Count == 0)
        {
            return;
        }

        var sqlTransaction = RequireTransaction(transaction);
        var metadata = _metadata.GetMetadata<T>();
        var versionColumn = GetRequiredColumn(metadata, nameof(DBModel.Version));
        var updateColumns = metadata.PersistedColumns
            .Where(column => !column.IsCode)
            .Where(column => column.PropertyName != nameof(DBModel.CreatedAt))
            .Where(column => column.PropertyName != nameof(DBModel.CreatedBy))
            .Where(column => column.PropertyName != nameof(DBModel.Version))
            .Where(column => !column.IsTenantCode)
            .ToArray();

        if (updateColumns.Length == 0)
        {
            return;
        }

        var stagingColumns = new List<DBColumnMetadata>
        {
            metadata.CodeColumn,
            versionColumn
        };

        if (_options.MultiTenancy.Enabled && metadata.TenantScoped)
        {
            stagingColumns.Add(
                metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Multi-tenancy is enabled, but model '{metadata.ModelName}' has no tenant column."));
        }

        stagingColumns.AddRange(updateColumns);
        stagingColumns = stagingColumns
            .DistinctBy(column => column.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tempTable = $"#SimpleOrmUpdate_{Guid.NewGuid():N}";

        await CreateStagingTable(
            sqlTransaction,
            metadata.TableName,
            tempTable,
            stagingColumns,
            cancellationToken);

        var table = BuildBulkTable(models, stagingColumns);

        await BulkCopy(
            sqlTransaction,
            tempTable,
            table,
            stagingColumns,
            cancellationToken,
            useTableLock: false);

        var join = BuildStagingJoin(metadata, metadata.ConcurrencyEnabled);
        var assignments = updateColumns
            .Select(column =>
                $"[t].[{EscapeIdentifier(column.ColumnName)}] = [s].[{EscapeIdentifier(column.ColumnName)}]")
            .Append(
                $"[t].[{EscapeIdentifier(versionColumn.ColumnName)}] = [t].[{EscapeIdentifier(versionColumn.ColumnName)}] + 1");

        var activeFilter = metadata.HardDelete
            ? string.Empty
            : $" AND [t].[{EscapeIdentifier(GetRequiredColumn(metadata, nameof(DBModel.DeletedAt)).ColumnName)}] IS NULL";

        var sql = $"""
                  UPDATE [t]
                  SET {string.Join(", ", assignments)}
                  FROM [{EscapeIdentifier(metadata.TableName)}] AS [t]
                  INNER JOIN [{EscapeIdentifier(tempTable)}] AS [s]
                      ON {join}
                  WHERE 1 = 1{activeFilter};
                  SELECT @@ROWCOUNT;
                  DROP TABLE [{EscapeIdentifier(tempTable)}];
                  """;

        var affected = await ExecuteScalarInt64InTransaction(
            sqlTransaction,
            sql,
            cancellationToken);

        if (metadata.ConcurrencyEnabled && affected != models.Count)
        {
            throw new Exceptions.DBConcurrencyException(
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
        ArgumentNullException.ThrowIfNull(transaction);

        if (models.Count == 0)
        {
            return;
        }

        var sqlTransaction = RequireTransaction(transaction);
        var metadata = _metadata.GetMetadata<T>();
        var versionColumn = GetRequiredColumn(metadata, nameof(DBModel.Version));
        var stagingColumns = new List<DBColumnMetadata>
        {
            metadata.CodeColumn,
            versionColumn
        };

        if (_options.MultiTenancy.Enabled && metadata.TenantScoped)
        {
            stagingColumns.Add(
                metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Multi-tenancy is enabled, but model '{metadata.ModelName}' has no tenant column."));
        }

        if (!hardDelete)
        {
            stagingColumns.Add(GetRequiredColumn(metadata, nameof(DBModel.DeletedAt)));
            stagingColumns.Add(GetRequiredColumn(metadata, nameof(DBModel.UpdatedAt)));
            stagingColumns.Add(GetRequiredColumn(metadata, nameof(DBModel.UpdatedBy)));
        }

        stagingColumns = stagingColumns
            .DistinctBy(column => column.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tempTable = $"#SimpleOrmDelete_{Guid.NewGuid():N}";

        await CreateStagingTable(
            sqlTransaction,
            metadata.TableName,
            tempTable,
            stagingColumns,
            cancellationToken);

        var table = BuildBulkTable(models, stagingColumns);

        await BulkCopy(
            sqlTransaction,
            tempTable,
            table,
            stagingColumns,
            cancellationToken,
            useTableLock: false);

        var join = BuildStagingJoin(metadata, metadata.ConcurrencyEnabled);
        string sql;

        if (hardDelete)
        {
            sql = $"""
                  DELETE [t]
                  FROM [{EscapeIdentifier(metadata.TableName)}] AS [t]
                  INNER JOIN [{EscapeIdentifier(tempTable)}] AS [s]
                      ON {join};
                  SELECT @@ROWCOUNT;
                  DROP TABLE [{EscapeIdentifier(tempTable)}];
                  """;
        }
        else
        {
            var deletedAt = GetRequiredColumn(metadata, nameof(DBModel.DeletedAt));
            var updatedAt = GetRequiredColumn(metadata, nameof(DBModel.UpdatedAt));
            var updatedBy = GetRequiredColumn(metadata, nameof(DBModel.UpdatedBy));

            sql = $"""
                  UPDATE [t]
                  SET
                      [t].[{EscapeIdentifier(deletedAt.ColumnName)}] = [s].[{EscapeIdentifier(deletedAt.ColumnName)}],
                      [t].[{EscapeIdentifier(updatedAt.ColumnName)}] = [s].[{EscapeIdentifier(updatedAt.ColumnName)}],
                      [t].[{EscapeIdentifier(updatedBy.ColumnName)}] = [s].[{EscapeIdentifier(updatedBy.ColumnName)}],
                      [t].[{EscapeIdentifier(versionColumn.ColumnName)}] = [t].[{EscapeIdentifier(versionColumn.ColumnName)}] + 1
                  FROM [{EscapeIdentifier(metadata.TableName)}] AS [t]
                  INNER JOIN [{EscapeIdentifier(tempTable)}] AS [s]
                      ON {join}
                  WHERE [t].[{EscapeIdentifier(deletedAt.ColumnName)}] IS NULL;
                  SELECT @@ROWCOUNT;
                  DROP TABLE [{EscapeIdentifier(tempTable)}];
                  """;
        }

        var affected = await ExecuteScalarInt64InTransaction(
            sqlTransaction,
            sql,
            cancellationToken);

        if (metadata.ConcurrencyEnabled && affected != models.Count)
        {
            throw new Exceptions.DBConcurrencyException(
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                              DELETE FROM [{EscapeIdentifier(metadata.TableName)}]
                              WHERE [{EscapeIdentifier(createdAtColumn.ColumnName)}] < @cutoffUtc;
                              """;
        command.Parameters.AddWithValue(
            "@cutoffUtc",
            olderThanUtc);

        var affected = await command.ExecuteNonQueryAsync(
            cancellationToken);

        return affected;
    }

    /// <inheritdoc />
    public string GenerateDebugQuery<T>(
        SearchParam search,
        int skip = 0,
        int limit = 100)
        where T : DBModel
    {
        ArgumentNullException.ThrowIfNull(search);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skip),
                "Skip cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "Limit cannot be negative.");
        }

        var query = BuildSelectQuery<T>(
            search,
            skip,
            limit,
            single: false);

        return RenderDebugQuery(
            query.Sql,
            query.Parameters);
    }

    /// <inheritdoc />
    public async Task<dynamic?> QuerySingle(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await Query(
            query,
            parameters,
            cancellationToken);

        return rows.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<dynamic>> Query(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateRawCommand(
            connection,
            query,
            parameters);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        var rows = new List<dynamic>();

        while (await reader.ReadAsync(cancellationToken))
        {
            IDictionary<string, object?> row = new ExpandoObject();

            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingle<T>(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await Query<T>(
            query,
            parameters,
            cancellationToken);

        return rows.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> Query<T>(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateRawCommand(
            connection,
            query,
            parameters);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        var rows = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(
                MaterializeRaw<T>(reader));
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<long> Execute(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateRawCommand(
            connection,
            query,
            parameters);

        var affectedRows = await command.ExecuteNonQueryAsync(
            cancellationToken);

        return affectedRows;
    }

    private SqlConnection CreateConnection()
    {
        var connection = _options.Connection;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = connection.Port > 0
                ? $"{connection.Host},{connection.Port}"
                : connection.Host,
            InitialCatalog = connection.Database,
            Encrypt = connection.UseSsl,
            TrustServerCertificate = !connection.UseSsl
        };

        if (string.IsNullOrWhiteSpace(connection.Username))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = connection.Username;
            builder.Password = connection.Password ?? string.Empty;
        }

        ApplyConnectionOptions(
            builder,
            connection.Options);

        return new SqlConnection(
            builder.ConnectionString);
    }

    private static void ApplyConnectionOptions(
        SqlConnectionStringBuilder builder,
        IEnumerable<KeyValuePair<string, string>> options)
    {
        foreach (var option in options)
        {
            builder[option.Key] = option.Value;
        }
    }

    private (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildSelectQuery<T>(
        SearchParam search,
        int skip,
        int limit,
        bool single,
        bool dynamicProjection = false)
        where T : DBModel
    {
        var metadata = _metadata.GetMetadata<T>();
        var parameters = new Dictionary<string, object?>();

        var select = BuildSelectList(
            metadata,
            search,
            dynamicProjection);

        var joins = BuildFromClause(
            metadata,
            search);

        var where = BuildWhereClause(
            metadata,
            search,
            parameters);

        var orderBy = BuildOrderBy(
            metadata,
            search);

        var pagination = single
            ? string.Empty
            : limit == 0
                ? $"OFFSET {skip} ROWS"
                : $"OFFSET {skip} ROWS FETCH NEXT {limit} ROWS ONLY";

        var top = single
            ? "TOP (1) "
            : string.Empty;

        var sql = $"""
                   SELECT {top}{select}
                   FROM [{EscapeIdentifier(metadata.TableName)}] AS [t]
                   {joins}
                   {where}
                   {orderBy}
                   {pagination};
                   """;

        return (
            sql,
            parameters);
    }

    private string BuildSelectList(
        DBModelMetadata metadata,
        SearchParam search,
        bool dynamicProjection)
    {
        ValidateSelectedFields(
            metadata,
            search.Fields);

        IEnumerable<DBColumnMetadata> mainColumns = metadata.PersistedColumns;

        if (dynamicProjection || search.Fields.Count > 0)
        {
            mainColumns = mainColumns.Where(
                column => search.Fields.Contains(
                    column.PropertyName,
                    StringComparer.OrdinalIgnoreCase));
        }

        var fields = mainColumns
            .Select(
                column =>
                    dynamicProjection
                        ? $"[t].[{EscapeIdentifier(column.ColumnName)}] " +
                          $"AS [{EscapeIdentifier(column.PropertyName)}]"
                        : $"[t].[{EscapeIdentifier(column.ColumnName)}]")
            .ToList();

        for (var index = 0; index < search.Joins.Count; index++)
        {
            var join = search.Joins[index];
            var joinMetadata = _metadata.GetMetadata(join.Model);
            var alias = ResolveJoinAlias(
                join,
                index);

            ValidateSelectedFields(
                joinMetadata,
                join.Fields);

            IEnumerable<DBColumnMetadata> joinedColumns = joinMetadata.PersistedColumns;

            if (join.Fields.Count > 0)
            {
                joinedColumns = joinedColumns.Where(
                    column => join.Fields.Contains(
                        column.PropertyName,
                        StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                joinedColumns = Array.Empty<DBColumnMetadata>();
            }

            fields.AddRange(
                joinedColumns.Select(
                    column =>
                        $"[{EscapeIdentifier(alias)}].[{EscapeIdentifier(column.ColumnName)}] " +
                        $"AS [{EscapeIdentifier(alias + "_" + column.PropertyName)}]"));
        }

        if (fields.Count == 0)
        {
            throw new InvalidOperationException(
                $"No fields were selected for model '{metadata.ModelName}'.");
        }

        return string.Join(
            ", ",
            fields);
    }

    private static void ValidateSelectedFields(
        DBModelMetadata metadata,
        IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            if (!metadata.PersistedColumns.Any(
                    column => column.PropertyName.Equals(
                        field,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Property '{field}' is not a persisted field on model '{metadata.ModelName}'.");
            }
        }
    }

    private async Task<IReadOnlyList<dynamic>> ReadDynamic(
        (string Sql, IReadOnlyDictionary<string, object?> Parameters) query,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = query.Sql;

        AddParameters(
            command,
            query.Parameters);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        var rows = new List<dynamic>();

        while (await reader.ReadAsync(cancellationToken))
        {
            IDictionary<string, object?> row = new ExpandoObject();

            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private string BuildFromClause(
        DBModelMetadata mainMetadata,
        SearchParam search)
    {
        if (search.Joins.Count == 0)
        {
            return string.Empty;
        }

        var joins = new List<string>(search.Joins.Count);

        for (var index = 0; index < search.Joins.Count; index++)
        {
            var join = search.Joins[index];
            var joinMetadata = _metadata.GetMetadata(join.Model);

            var localColumn = FindColumn(
                mainMetadata,
                join.LocalField);

            var foreignColumn = FindColumn(
                joinMetadata,
                join.ForeignField);

            var alias = ResolveJoinAlias(
                join,
                index);

            var joinKeyword = join.Type switch
            {
                JoinType.Inner => "INNER JOIN",
                JoinType.LeftOuter => "LEFT OUTER JOIN",
                JoinType.RightOuter => "RIGHT OUTER JOIN",
                JoinType.FullOuter => "FULL OUTER JOIN",
                _ => throw new NotSupportedException(
                    $"Join type '{join.Type}' is not supported by the SQL Server provider.")
            };

            joins.Add(
                $"{joinKeyword} [{EscapeIdentifier(joinMetadata.TableName)}] " +
                $"AS [{EscapeIdentifier(alias)}] " +
                $"ON [t].[{EscapeIdentifier(localColumn.ColumnName)}] = " +
                $"[{EscapeIdentifier(alias)}].[{EscapeIdentifier(foreignColumn.ColumnName)}]");
        }

        return string.Join(
            Environment.NewLine,
            joins);
    }

    private string BuildWhereClause(
        DBModelMetadata metadata,
        SearchParam search,
        Dictionary<string, object?> parameters)
    {
        var userConditions = new List<string>();

        foreach (var filter in search.Filters)
        {
            userConditions.Add(
                BuildFilter(
                    metadata,
                    filter,
                    parameters));
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var searchableColumns = metadata.SearchableColumns;

            if (search.SearchFields.Count > 0)
            {
                searchableColumns = searchableColumns.Where(
                    column => search.SearchFields.Contains(
                        column.PropertyName,
                        StringComparer.OrdinalIgnoreCase));
            }

            var columns = searchableColumns.ToArray();

            if (columns.Length > 0)
            {
                var searchParameter = AddParameter(
                    parameters,
                    $"%{search.Search}%");

                var searchConditions = columns.Select(
                    column =>
                        $"[t].[{EscapeIdentifier(column.ColumnName)}] LIKE {searchParameter}");

                userConditions.Add(
                    $"({string.Join(" OR ", searchConditions)})");
            }
        }

        var requiredConditions = new List<string>();

        if (_options.MultiTenancy.Enabled
            && metadata.TenantScoped)
        {
            var tenantColumn = metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Multi-tenancy is enabled, but model '{metadata.ModelName}' has no tenant column.");

            var tenantCode = _tenant.GetTenant();

            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                throw new InvalidOperationException(
                    "A tenant code is required for the current database operation.");
            }

            var tenantParameter = AddParameter(
                parameters,
                tenantCode);

            requiredConditions.Add(
                $"[t].[{EscapeIdentifier(tenantColumn.ColumnName)}] = {tenantParameter}");
        }

        if (!metadata.HardDelete && !search.IncludeDeleted)
        {
            var deletedAtColumn = GetRequiredColumn(
                metadata,
                nameof(DBModel.DeletedAt));

            requiredConditions.Add(
                $"[t].[{EscapeIdentifier(deletedAtColumn.ColumnName)}] IS NULL");
        }

        var conditions = new List<string>();

        if (userConditions.Count > 0)
        {
            var separator = search.Condition == SearchCondition.Or
                ? " OR "
                : " AND ";

            conditions.Add(
                $"({string.Join(separator, userConditions)})");
        }

        conditions.AddRange(
            requiredConditions);

        return conditions.Count == 0
            ? string.Empty
            : $"WHERE {string.Join(" AND ", conditions)}";
    }

    private string BuildFilter(
        DBModelMetadata metadata,
        SearchFilter filter,
        Dictionary<string, object?> parameters)
    {
        var column = FindColumn(
            metadata,
            filter.Field);

        var name = $"[t].[{EscapeIdentifier(column.ColumnName)}]";

        return filter.Operator switch
        {
            SearchOperator.EQ =>
                $"{name} = {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.NEQ =>
                $"{name} <> {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.GT =>
                $"{name} > {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.GTE =>
                $"{name} >= {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.LT =>
                $"{name} < {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.LTE =>
                $"{name} <= {AddParameter(parameters, ToDatabaseValue(column, filter.Value))}",

            SearchOperator.Contains =>
                $"{name} LIKE {AddParameter(parameters, $"%{filter.Value}%")}",

            SearchOperator.StartsWith =>
                $"{name} LIKE {AddParameter(parameters, $"{filter.Value}%")}",

            SearchOperator.EndsWith =>
                $"{name} LIKE {AddParameter(parameters, $"%{filter.Value}")}",

            SearchOperator.IsNull =>
                $"{name} IS NULL",

            SearchOperator.IsNotNull =>
                $"{name} IS NOT NULL",

            SearchOperator.In =>
                BuildInFilter(
                    name,
                    filter.Value,
                    parameters,
                    column,
                    negate: false),

            SearchOperator.NotIn =>
                BuildInFilter(
                    name,
                    filter.Value,
                    parameters,
                    column,
                    negate: true),

            SearchOperator.Between =>
                BuildBetweenFilter(
                    name,
                    filter.Value,
                    parameters,
                    column,
                    negate: false),

            SearchOperator.NotBetween =>
                BuildBetweenFilter(
                    name,
                    filter.Value,
                    parameters,
                    column,
                    negate: true),

            _ => throw new NotSupportedException(
                $"Search operator '{filter.Operator}' is not supported by the SQL Server provider.")
        };
    }

    private static string BuildInFilter(
        string columnName,
        object? value,
        Dictionary<string, object?> parameters,
        DBColumnMetadata column,
        bool negate)
    {
        if (value is string || value is not IEnumerable enumerable)
        {
            throw new ArgumentException(
                $"{(negate ? "NOT IN" : "IN")} requires an enumerable value.");
        }

        var parameterNames = enumerable
            .Cast<object?>()
            .Select(
                item => AddParameter(
                    parameters,
                    ToDatabaseValue(
                        column,
                        item)))
            .ToArray();

        if (parameterNames.Length == 0)
        {
            return negate
                ? "1 = 1"
                : "1 = 0";
        }

        return $"{columnName} {(negate ? "NOT IN" : "IN")} " +
               $"({string.Join(", ", parameterNames)})";
    }

    private static string BuildBetweenFilter(
        string columnName,
        object? value,
        Dictionary<string, object?> parameters,
        DBColumnMetadata column,
        bool negate)
    {
        if (value is string || value is not IEnumerable enumerable)
        {
            throw new ArgumentException(
                $"{(negate ? "NOT BETWEEN" : "BETWEEN")} requires exactly two values.");
        }

        var values = enumerable
            .Cast<object?>()
            .Take(3)
            .ToArray();

        if (values.Length != 2)
        {
            throw new ArgumentException(
                $"{(negate ? "NOT BETWEEN" : "BETWEEN")} requires exactly two values.");
        }

        var first = AddParameter(
            parameters,
            ToDatabaseValue(
                column,
                values[0]));

        var second = AddParameter(
            parameters,
            ToDatabaseValue(
                column,
                values[1]));

        return $"{columnName} {(negate ? "NOT BETWEEN" : "BETWEEN")} {first} AND {second}";
    }

    private string BuildOrderBy(
        DBModelMetadata metadata,
        SearchParam search)
    {
        var orderParts = new List<string>();

        foreach (var order in search.OrderBy)
        {
            var column = FindColumn(
                metadata,
                order.Field);

            orderParts.Add(
                $"[t].[{EscapeIdentifier(column.ColumnName)}] " +
                $"{(order.Descending ? "DESC" : "ASC")}");
        }

        if (!search.OrderBy.Any(
                order => order.Field.Equals(
                    nameof(DBModel.Code),
                    StringComparison.OrdinalIgnoreCase)))
        {
            orderParts.Add(
                $"[t].[{EscapeIdentifier(metadata.CodeColumn.ColumnName)}] ASC");
        }

        return $"ORDER BY {string.Join(", ", orderParts)}";
    }

    private async Task<IReadOnlyList<T>> ReadModels<T>(
        (string Sql, IReadOnlyDictionary<string, object?> Parameters) query,
        CancellationToken cancellationToken)
        where T : DBModel
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = query.Sql;

        AddParameters(
            command,
            query.Parameters);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        var metadata = _metadata.GetMetadata<T>();
        var rows = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(
                MaterializeModel<T>(
                    reader,
                    metadata));
        }

        return rows;
    }

    private static T MaterializeModel<T>(
        SqlDataReader reader,
        DBModelMetadata metadata)
        where T : DBModel
    {
        var model = (T)(Activator.CreateInstance(
            typeof(T),
            nonPublic: true)
            ?? throw new InvalidOperationException(
                $"Could not create an instance of '{typeof(T).FullName}'. " +
                "A parameterless constructor is required for SQL Server materialization."));

        for (var index = 0; index < reader.FieldCount; index++)
        {
            var databaseName = reader.GetName(index);

            var column = metadata.Columns.FirstOrDefault(
                candidate => candidate.ColumnName.Equals(
                    databaseName,
                    StringComparison.OrdinalIgnoreCase));

            if (column is null || column.Ignore || reader.IsDBNull(index))
            {
                continue;
            }

            var converted = ConvertToClrValue(
                reader.GetValue(index),
                column.PropertyType,
                column.IsEnum,
                column.UnderlyingType,
                column.EnumStorage);

            column.Property.SetValue(
                model,
                converted);
        }

        model.DataState = DataState.Unchanged;

        return model;
    }

    private static T MaterializeRaw<T>(
        SqlDataReader reader)
    {
        var targetType = typeof(T);

        if (IsSimpleType(targetType) && reader.FieldCount > 0)
        {
            var value = reader.IsDBNull(0)
                ? null
                : reader.GetValue(0);

            var converted = ConvertToClrValue(
                value,
                targetType,
                targetType.IsEnum,
                Nullable.GetUnderlyingType(targetType) ?? targetType,
                null);

            return converted is null
                ? default!
                : (T)converted;
        }

        var instance = Activator.CreateInstance(
            targetType,
            nonPublic: true)
            ?? throw new InvalidOperationException(
                $"Could not create an instance of '{targetType.FullName}'. " +
                "A parameterless constructor is required for typed raw-query materialization.");

        var properties = targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToDictionary(
                property => property.Name,
                StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (reader.IsDBNull(index))
            {
                continue;
            }

            if (!properties.TryGetValue(
                    reader.GetName(index),
                    out var property))
            {
                continue;
            }

            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType)
                ?? propertyType;

            var converted = ConvertToClrValue(
                reader.GetValue(index),
                propertyType,
                underlyingType.IsEnum,
                underlyingType,
                null);

            property.SetValue(
                instance,
                converted);
        }

        return (T)instance;
    }

    private static object? ConvertToClrValue(
        object? value,
        Type propertyType,
        bool isEnum,
        Type underlyingType,
        EnumStorage? enumStorage)
    {
        if (value is null || value == DBNull.Value)
        {
            if (Nullable.GetUnderlyingType(propertyType) is not null
                || !propertyType.IsValueType)
            {
                return null;
            }

            return Activator.CreateInstance(propertyType);
        }

        if (isEnum)
        {
            if (enumStorage == EnumStorage.String || value is string)
            {
                return Enum.Parse(
                    underlyingType,
                    value.ToString()!,
                    ignoreCase: true);
            }

            return Enum.ToObject(
                underlyingType,
                value);
        }

        if (underlyingType == typeof(Guid))
        {
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!);
        }

        if (underlyingType == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(
                    value.ToString()!,
                    CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(DateOnly))
        {
            return value switch
            {
                DateOnly dateOnly => dateOnly,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                _ => DateOnly.Parse(
                    value.ToString()!,
                    CultureInfo.InvariantCulture)
            };
        }

        if (underlyingType == typeof(TimeOnly))
        {
            return value switch
            {
                TimeOnly timeOnly => timeOnly,
                TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
                DateTime dateTime => TimeOnly.FromDateTime(dateTime),
                _ => TimeOnly.Parse(
                    value.ToString()!,
                    CultureInfo.InvariantCulture)
            };
        }

        if (underlyingType == typeof(byte[]))
        {
            return value;
        }

        if (underlyingType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        return Convert.ChangeType(
            value,
            underlyingType,
            CultureInfo.InvariantCulture);
    }

    private static object? ToDatabaseValue(
        DBColumnMetadata column,
        object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!column.IsEnum || value is not Enum enumValue)
        {
            return value;
        }

        return column.EnumStorage == EnumStorage.String
            ? enumValue.ToString()
            : Convert.ToInt32(
                enumValue,
                CultureInfo.InvariantCulture);
    }

    private void AddTenantWriteCondition(
        DBModelMetadata metadata,
        Dictionary<string, object?> parameters,
        ICollection<string> whereParts)
    {
        if (!_options.MultiTenancy.Enabled
            || !metadata.TenantScoped)
        {
            return;
        }

        var tenantColumn = metadata.TenantColumn
            ?? throw new InvalidOperationException(
                $"Multi-tenancy is enabled, but model '{metadata.ModelName}' has no tenant column.");

        var tenantCode = _tenant.GetTenant();

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new InvalidOperationException(
                "A tenant code is required for the current database operation.");
        }

        var tenantParameter = AddParameter(
            parameters,
            tenantCode);

        whereParts.Add(
            $"[{EscapeIdentifier(tenantColumn.ColumnName)}] = {tenantParameter}");
    }

    private static DBColumnMetadata FindColumn(
        DBModelMetadata metadata,
        string propertyName)
    {
        var column = metadata.PersistedColumns.FirstOrDefault(
            candidate => candidate.PropertyName.Equals(
                propertyName,
                StringComparison.OrdinalIgnoreCase));

        return column
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' is not a persisted column on model '{metadata.ModelName}'.");
    }

    private static DBColumnMetadata GetRequiredColumn(
        DBModelMetadata metadata,
        string propertyName)
    {
        return FindColumn(
            metadata,
            propertyName);
    }

    private static string ResolveJoinAlias(
        SearchJoin join,
        int index)
    {
        var alias = string.IsNullOrWhiteSpace(join.Alias)
            ? $"{join.Model.Name}_{index}"
            : join.Alias.Trim();

        return alias;
    }

    private static DataTable BuildBulkTable<T>(
        IReadOnlyList<T> models,
        IReadOnlyList<DBColumnMetadata> columns)
        where T : DBModel
    {
        var table = new DataTable();

        foreach (var column in columns)
        {
            table.Columns.Add(
                column.ColumnName,
                GetBulkClrType(column));
        }

        foreach (var model in models)
        {
            var row = table.NewRow();

            foreach (var column in columns)
            {
                row[column.ColumnName] =
                    ToDatabaseValue(
                        column,
                        column.Property.GetValue(model))
                    ?? DBNull.Value;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private static Type GetBulkClrType(DBColumnMetadata column)
    {
        if (column.IsEnum)
        {
            return column.EnumStorage == EnumStorage.String
                ? typeof(string)
                : typeof(int);
        }

        return column.UnderlyingType;
    }

    private static async Task BulkCopy(
        SqlTx transaction,
        string destinationTable,
        DataTable table,
        IReadOnlyList<DBColumnMetadata> columns,
        CancellationToken cancellationToken,
        bool useTableLock = true)
    {
        var options = SqlBulkCopyOptions.CheckConstraints
                      | SqlBulkCopyOptions.KeepNulls;

        if (useTableLock && table.Rows.Count >= 100)
        {
            options |= SqlBulkCopyOptions.TableLock;
        }

        using var bulkCopy = new SqlBulkCopy(
            transaction.Connection,
            options,
            transaction.Transaction)
        {
            DestinationTableName = destinationTable,
            BatchSize = Math.Max(1, table.Rows.Count),
            EnableStreaming = true
        };

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(
                column.ColumnName,
                column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(
            table,
            cancellationToken);
    }

    private static async Task CreateStagingTable(
        SqlTx transaction,
        string sourceTable,
        string stagingTable,
        IReadOnlyList<DBColumnMetadata> columns,
        CancellationToken cancellationToken)
    {
        var projection = string.Join(
            ", ",
            columns.Select(column =>
                $"[{EscapeIdentifier(column.ColumnName)}]"));

        var sql = $"""
                  SELECT TOP (0) {projection}
                  INTO [{EscapeIdentifier(stagingTable)}]
                  FROM [{EscapeIdentifier(sourceTable)}];
                  """;

        await ExecuteInTransaction(
            transaction,
            sql,
            new Dictionary<string, object?>(),
            cancellationToken);
    }

    private string BuildStagingJoin(
        DBModelMetadata metadata,
        bool includeConcurrency = false)
    {
        var parts = new List<string>
        {
            $"[t].[{EscapeIdentifier(metadata.CodeColumn.ColumnName)}] = [s].[{EscapeIdentifier(metadata.CodeColumn.ColumnName)}]"
        };

        if (_options.MultiTenancy.Enabled && metadata.TenantScoped)
        {
            var tenantColumn = metadata.TenantColumn
                ?? throw new InvalidOperationException(
                    $"Multi-tenancy is enabled, but model '{metadata.ModelName}' has no tenant column.");

            parts.Add(
                $"[t].[{EscapeIdentifier(tenantColumn.ColumnName)}] = [s].[{EscapeIdentifier(tenantColumn.ColumnName)}]");
        }

        if (includeConcurrency)
        {
            var versionColumn = GetRequiredColumn(
                metadata,
                nameof(DBModel.Version));

            parts.Add(
                $"[t].[{EscapeIdentifier(versionColumn.ColumnName)}] = [s].[{EscapeIdentifier(versionColumn.ColumnName)}]");
        }

        return string.Join(" AND ", parts);
    }

    private static SqlTx RequireTransaction(
        IDBTransaction transaction)
    {
        return transaction as SqlTx
            ?? throw new InvalidOperationException(
                "The supplied transaction was not created by the SQL Server provider.");
    }

    private static async Task<long> ExecuteScalarInt64InTransaction(
        SqlTx transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction.Transaction;
        command.CommandText = sql;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteInTransaction(
        SqlTx transaction,
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction.Transaction;
        command.CommandText = sql;

        AddParameters(
            command,
            parameters);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static SqlCommand CreateRawCommand(
        SqlConnection connection,
        string query,
        object? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = query;

        AddRawParameters(
            command,
            parameters);

        return command;
    }

    private static void AddRawParameters(
        SqlCommand command,
        object? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        if (parameters is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var parameter in readOnlyDictionary)
            {
                AddRawParameter(
                    command,
                    parameter.Key,
                    parameter.Value);
            }

            return;
        }

        if (parameters is IDictionary<string, object?> dictionary)
        {
            foreach (var parameter in dictionary)
            {
                AddRawParameter(
                    command,
                    parameter.Key,
                    parameter.Value);
            }

            return;
        }

        foreach (var property in parameters.GetType().GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            AddRawParameter(
                command,
                property.Name,
                property.GetValue(parameters));
        }
    }

    private static void AddRawParameter(
        SqlCommand command,
        string name,
        object? value)
    {
        var parameterName = name.StartsWith(
            "@",
            StringComparison.Ordinal)
            ? name
            : "@" + name;

        command.Parameters.AddWithValue(
            parameterName,
            value ?? DBNull.Value);
    }

    private static void AddParameters(
        SqlCommand command,
        IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Key,
                parameter.Value ?? DBNull.Value);
        }
    }

    private static string AddParameter(
        IDictionary<string, object?> parameters,
        object? value)
    {
        var name = $"@p{parameters.Count}";

        parameters[name] = value;

        return name;
    }

    private static string RenderDebugQuery(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var result = sql;

        foreach (var parameter in parameters.OrderByDescending(
                     item => item.Key.Length))
        {
            result = result.Replace(
                parameter.Key,
                FormatDebugValue(parameter.Value),
                StringComparison.Ordinal);
        }

        return result;
    }

    private static string FormatDebugValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            DBNull _ => "NULL",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            char character => $"'{character.ToString().Replace("'", "''", StringComparison.Ordinal)}'",
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => $"'{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset:O}'",
            DateOnly dateOnly => $"'{dateOnly:yyyy-MM-dd}'",
            TimeOnly timeOnly => $"'{timeOnly:HH:mm:ss.fffffff}'",
            Guid guid => $"'{guid}'",
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            Enum enumValue => $"'{enumValue}'",
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{value.ToString()?.Replace("'", "''", StringComparison.Ordinal)}'"
        };
    }

    private static bool IsSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type)
            ?? type;

        return underlyingType.IsPrimitive
               || underlyingType.IsEnum
               || underlyingType == typeof(string)
               || underlyingType == typeof(decimal)
               || underlyingType == typeof(Guid)
               || underlyingType == typeof(DateTime)
               || underlyingType == typeof(DateTimeOffset)
               || underlyingType == typeof(DateOnly)
               || underlyingType == typeof(TimeOnly);
    }

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(
                "A SQL identifier cannot be empty.",
                nameof(identifier));
        }

        return identifier.Replace(
            "]",
            "]]",
            StringComparison.Ordinal);
    }
}
