using System.Globalization;
using Microsoft.Data.SqlClient;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.SqlServer;

/// <summary>
/// Synchronizes SQL Server tables, columns, primary keys and ORM-managed unique indexes
/// with the cached <see cref="DBModelMetadata"/> definitions.
/// </summary>
public sealed class SqlServerSchemaSynchronizer : IDBSchemaSynchronizer
{
    private const string MigrationLogTable = "__DBMigrationLog";
    private const string ManagedIndexPrefix = "SORM_";

    private readonly IDBMetadataProvider _metadata;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerSchemaSynchronizer"/> class.
    /// </summary>
    /// <param name="metadata">The cached model metadata provider.</param>
    /// <param name="options">The SimpleORM options.</param>
    public SqlServerSchemaSynchronizer(
        IDBMetadataProvider metadata,
        SimpleOrmOptions options)
    {
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationLogTable(
            connection,
            cancellationToken);

        foreach (var model in _metadata
                     .GetRegisteredModels()
                     .OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await SynchronizeModel(
                    connection,
                    model,
                    cancellationToken);
            }
            catch when (_options.Migrations.FailureMode == MigrationFailureMode.LogAndContinue)
            {
                // The individual schema operation already writes its failure to the
                // migration log. LogAndContinue deliberately allows startup to proceed.
            }
        }
    }

    private async Task SynchronizeModel(
        SqlConnection connection,
        DBModelMetadata model,
        CancellationToken cancellationToken)
    {
        if (!await TableExists(
                connection,
                model.TableName,
                cancellationToken))
        {
            var createSql = BuildCreateTableSql(model);

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "CreateTable",
                model.TableName,
                createSql,
                cancellationToken);

            await SynchronizeUniqueIndexes(
                connection,
                model,
                cancellationToken);

            return;
        }

        var databaseColumns = await ReadColumns(
            connection,
            model.TableName,
            cancellationToken);

        await AddMissingColumns(
            connection,
            model,
            databaseColumns,
            cancellationToken);

        await AlterChangedColumns(
            connection,
            model,
            databaseColumns,
            cancellationToken);

        if (_options.Migrations.AllowDestructiveChanges)
        {
            await DropRemovedColumns(
                connection,
                model,
                databaseColumns,
                cancellationToken);
        }

        await EnsureCodePrimaryKey(
            connection,
            model,
            cancellationToken);

        await SynchronizeUniqueIndexes(
            connection,
            model,
            cancellationToken);
    }

    private async Task AddMissingColumns(
        SqlConnection connection,
        DBModelMetadata model,
        IReadOnlyDictionary<string, (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable)> databaseColumns,
        CancellationToken cancellationToken)
    {
        foreach (var column in model.PersistedColumns)
        {
            if (databaseColumns.ContainsKey(column.ColumnName))
            {
                continue;
            }

            var isVersionColumn = column.PropertyName == nameof(DBModel.Version);
            var tableHasRows = !IsNullable(column)
                && await TableHasRows(
                    connection,
                    model.TableName,
                    cancellationToken);

            if (tableHasRows && !isVersionColumn)
            {
                throw new InvalidOperationException(
                    $"Cannot automatically add required column '{model.TableName}.{column.ColumnName}' " +
                    "to a table that already contains data because no default value was defined.");
            }

            var versionDefault = isVersionColumn
                ? " DEFAULT (1)"
                : string.Empty;

            var sql = $"""
                       ALTER TABLE [{EscapeIdentifier(model.TableName)}]
                       ADD [{EscapeIdentifier(column.ColumnName)}]
                           {GetSqlType(column)}
                           {(IsNullable(column) ? "NULL" : "NOT NULL")}{versionDefault};
                       """;

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "AddColumn",
                $"{model.TableName}.{column.ColumnName}",
                sql,
                cancellationToken);
        }
    }

    private async Task AlterChangedColumns(
        SqlConnection connection,
        DBModelMetadata model,
        IReadOnlyDictionary<string, (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable)> databaseColumns,
        CancellationToken cancellationToken)
    {
        foreach (var column in model.PersistedColumns)
        {
            if (!databaseColumns.TryGetValue(
                    column.ColumnName,
                    out var existing))
            {
                continue;
            }

            var expected = CreateExpectedColumn(column);

            if (ColumnsMatch(
                    existing,
                    expected))
            {
                continue;
            }

            var destructive = IsDestructiveChange(
                existing,
                expected);

            if (destructive && !_options.Migrations.AllowDestructiveChanges)
            {
                continue;
            }

            if (!expected.Nullable
                && existing.Nullable
                && await ColumnContainsNull(
                    connection,
                    model.TableName,
                    column.ColumnName,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Cannot make '{model.TableName}.{column.ColumnName}' NOT NULL because existing rows contain NULL values.");
            }

            var sql = $"""
                       ALTER TABLE [{EscapeIdentifier(model.TableName)}]
                       ALTER COLUMN [{EscapeIdentifier(column.ColumnName)}]
                           {GetSqlType(column)}
                           {(IsNullable(column) ? "NULL" : "NOT NULL")};
                       """;

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "AlterColumn",
                $"{model.TableName}.{column.ColumnName}",
                sql,
                cancellationToken);
        }
    }

    private async Task DropRemovedColumns(
        SqlConnection connection,
        DBModelMetadata model,
        IReadOnlyDictionary<string, (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable)> databaseColumns,
        CancellationToken cancellationToken)
    {
        var expectedColumns = model.PersistedColumns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var databaseColumn in databaseColumns.Values)
        {
            if (expectedColumns.Contains(databaseColumn.Name))
            {
                continue;
            }

            var sql = $"""
                       ALTER TABLE [{EscapeIdentifier(model.TableName)}]
                       DROP COLUMN [{EscapeIdentifier(databaseColumn.Name)}];
                       """;

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "DropColumn",
                $"{model.TableName}.{databaseColumn.Name}",
                sql,
                cancellationToken);
        }
    }

    private async Task EnsureCodePrimaryKey(
        SqlConnection connection,
        DBModelMetadata model,
        CancellationToken cancellationToken)
    {
        var currentPrimaryKeyColumns = await ReadPrimaryKeyColumns(
            connection,
            model.TableName,
            cancellationToken);

        if (currentPrimaryKeyColumns.Count == 1
            && currentPrimaryKeyColumns[0].Equals(
                model.CodeColumn.ColumnName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (currentPrimaryKeyColumns.Count > 0)
        {
            if (!_options.Migrations.AllowDestructiveChanges)
            {
                return;
            }

            var primaryKeyName = await ReadPrimaryKeyName(
                connection,
                model.TableName,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(primaryKeyName))
            {
                var dropSql = $"""
                               ALTER TABLE [{EscapeIdentifier(model.TableName)}]
                               DROP CONSTRAINT [{EscapeIdentifier(primaryKeyName)}];
                               """;

                await ExecuteSchemaChange(
                    connection,
                    model.ModelName,
                    "DropPrimaryKey",
                    model.TableName,
                    dropSql,
                    cancellationToken);
            }
        }

        var constraintName = BuildPrimaryKeyName(
            model.TableName);

        var createSql = $"""
                         ALTER TABLE [{EscapeIdentifier(model.TableName)}]
                         ADD CONSTRAINT [{EscapeIdentifier(constraintName)}]
                         PRIMARY KEY ([{EscapeIdentifier(model.CodeColumn.ColumnName)}]);
                         """;

        await ExecuteSchemaChange(
            connection,
            model.ModelName,
            "AddPrimaryKey",
            $"{model.TableName}.{model.CodeColumn.ColumnName}",
            createSql,
            cancellationToken);
    }

    private async Task SynchronizeUniqueIndexes(
        SqlConnection connection,
        DBModelMetadata model,
        CancellationToken cancellationToken)
    {
        var desiredIndexes = BuildDesiredUniqueIndexes(model);

        var existingIndexes = await ReadManagedIndexes(
            connection,
            model.TableName,
            cancellationToken);

        foreach (var desired in desiredIndexes.Values)
        {
            var hasExistingIndex = existingIndexes.TryGetValue(
                desired.Name,
                out var existing);

            if (hasExistingIndex
                && existing.Unique
                && existing.Columns.SequenceEqual(
                    desired.Columns,
                    StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hasExistingIndex)
            {
                var dropSql = $"""
                               DROP INDEX [{EscapeIdentifier(existing.Name)}]
                               ON [{EscapeIdentifier(model.TableName)}];
                               """;

                await ExecuteSchemaChange(
                    connection,
                    model.ModelName,
                    "DropIndex",
                    existing.Name,
                    dropSql,
                    cancellationToken);
            }

            var columnList = string.Join(
                ", ",
                desired.Columns.Select(
                    column => $"[{EscapeIdentifier(column)}]"));

            var createSql = $"""
                             CREATE UNIQUE INDEX [{EscapeIdentifier(desired.Name)}]
                             ON [{EscapeIdentifier(model.TableName)}] ({columnList});
                             """;

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "CreateUniqueIndex",
                desired.Name,
                createSql,
                cancellationToken);
        }

        if (!_options.Migrations.AllowDestructiveChanges)
        {
            return;
        }

        foreach (var existing in existingIndexes.Values)
        {
            if (desiredIndexes.ContainsKey(existing.Name))
            {
                continue;
            }

            var dropSql = $"""
                           DROP INDEX [{EscapeIdentifier(existing.Name)}]
                           ON [{EscapeIdentifier(model.TableName)}];
                           """;

            await ExecuteSchemaChange(
                connection,
                model.ModelName,
                "DropIndex",
                existing.Name,
                dropSql,
                cancellationToken);
        }
    }

    private static IReadOnlyDictionary<string, (string Name, bool Unique, IReadOnlyList<string> Columns)> BuildDesiredUniqueIndexes(
        DBModelMetadata model)
    {
        var indexes = new Dictionary<
            string,
            (string Name, bool Unique, IReadOnlyList<string> Columns)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var column in model.PersistedColumns
                     .Where(item => item.Unique && !item.IsCode)
                     .Where(item => string.IsNullOrWhiteSpace(item.UniqueGroup)))
        {
            var name = BuildUniqueIndexName(
                model.TableName,
                column.ColumnName);

            indexes[name] = (
                Name: name,
                Unique: true,
                Columns: new[] { column.ColumnName });
        }

        foreach (var group in model.PersistedColumns
                     .Where(item => item.Unique && !string.IsNullOrWhiteSpace(item.UniqueGroup))
                     .GroupBy(
                         item => item.UniqueGroup!,
                         StringComparer.OrdinalIgnoreCase))
        {
            var columns = group
                .Select(item => item.ColumnName)
                .ToArray();

            var name = BuildCompositeUniqueIndexName(
                model.TableName,
                group.Key);

            indexes[name] = (
                Name: name,
                Unique: true,
                Columns: columns);
        }

        return indexes;
    }

    private static string BuildCreateTableSql(
        DBModelMetadata model)
    {
        var definitions = new List<string>();

        foreach (var column in model.PersistedColumns)
        {
            definitions.Add(
                $"[{EscapeIdentifier(column.ColumnName)}] " +
                $"{GetSqlType(column)} " +
                $"{(IsNullable(column) ? "NULL" : "NOT NULL")}");
        }

        var primaryKeyName = BuildPrimaryKeyName(
            model.TableName);

        definitions.Add(
            $"CONSTRAINT [{EscapeIdentifier(primaryKeyName)}] " +
            $"PRIMARY KEY ([{EscapeIdentifier(model.CodeColumn.ColumnName)}])");

        return $"""
                CREATE TABLE [{EscapeIdentifier(model.TableName)}]
                (
                    {string.Join("," + Environment.NewLine + "    ", definitions)}
                );
                """;
    }

    private static async Task<bool> TableExists(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT CASE
                                  WHEN OBJECT_ID(@tableName, 'U') IS NULL THEN 0
                                  ELSE 1
                              END;
                              """;

        command.Parameters.AddWithValue(
            "@tableName",
            tableName);

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> TableHasRows(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
                               SELECT CASE
                                   WHEN EXISTS
                                   (
                                       SELECT TOP (1) 1
                                       FROM [{EscapeIdentifier(tableName)}]
                                   )
                                   THEN 1
                                   ELSE 0
                               END;
                               """;

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> ColumnContainsNull(
        SqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
                               SELECT CASE
                                   WHEN EXISTS
                                   (
                                       SELECT TOP (1) 1
                                       FROM [{EscapeIdentifier(tableName)}]
                                       WHERE [{EscapeIdentifier(columnName)}] IS NULL
                                   )
                                   THEN 1
                                   ELSE 0
                               END;
                               """;

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        return Convert.ToInt32(
            result,
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<IReadOnlyDictionary<string, (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable)>> ReadColumns(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT
                                  c.name,
                                  t.name AS type_name,
                                  c.max_length,
                                  c.precision,
                                  c.scale,
                                  c.is_nullable
                              FROM sys.columns AS c
                              INNER JOIN sys.types AS t
                                  ON c.user_type_id = t.user_type_id
                              WHERE c.object_id = OBJECT_ID(@tableName, 'U');
                              """;

        command.Parameters.AddWithValue(
            "@tableName",
            tableName);

        var columns = new Dictionary<
            string,
            (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable)>(
            StringComparer.OrdinalIgnoreCase);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var typeName = reader.GetString(1);
            var maxLength = reader.GetInt16(2);

            var characterLength = typeName.Equals(
                "nvarchar",
                StringComparison.OrdinalIgnoreCase)
                ? maxLength == -1
                    ? -1
                    : maxLength / 2
                : maxLength;

            var column = (
                Name: reader.GetString(0),
                TypeName: typeName,
                Length: characterLength,
                Precision: reader.GetByte(3),
                Scale: reader.GetByte(4),
                Nullable: reader.GetBoolean(5));

            columns[column.Name] = column;
        }

        return columns;
    }

    private static async Task<IReadOnlyList<string>> ReadPrimaryKeyColumns(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT c.name
                              FROM sys.key_constraints AS kc
                              INNER JOIN sys.index_columns AS ic
                                  ON kc.parent_object_id = ic.object_id
                                  AND kc.unique_index_id = ic.index_id
                              INNER JOIN sys.columns AS c
                                  ON ic.object_id = c.object_id
                                  AND ic.column_id = c.column_id
                              WHERE kc.parent_object_id = OBJECT_ID(@tableName, 'U')
                                AND kc.type = 'PK'
                              ORDER BY ic.key_ordinal;
                              """;

        command.Parameters.AddWithValue(
            "@tableName",
            tableName);

        var columns = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(
                reader.GetString(0));
        }

        return columns;
    }

    private static async Task<string?> ReadPrimaryKeyName(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT kc.name
                              FROM sys.key_constraints AS kc
                              WHERE kc.parent_object_id = OBJECT_ID(@tableName, 'U')
                                AND kc.type = 'PK';
                              """;

        command.Parameters.AddWithValue(
            "@tableName",
            tableName);

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        return result is null || result == DBNull.Value
            ? null
            : Convert.ToString(
                result,
                CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyDictionary<string, (string Name, bool Unique, IReadOnlyList<string> Columns)>> ReadManagedIndexes(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
                              SELECT
                                  i.name,
                                  i.is_unique,
                                  c.name,
                                  ic.key_ordinal
                              FROM sys.indexes AS i
                              INNER JOIN sys.index_columns AS ic
                                  ON i.object_id = ic.object_id
                                  AND i.index_id = ic.index_id
                              INNER JOIN sys.columns AS c
                                  ON ic.object_id = c.object_id
                                  AND ic.column_id = c.column_id
                              WHERE i.object_id = OBJECT_ID(@tableName, 'U')
                                AND i.name LIKE @prefix
                                AND i.is_primary_key = 0
                                AND ic.is_included_column = 0
                              ORDER BY i.name, ic.key_ordinal;
                              """;

        command.Parameters.AddWithValue(
            "@tableName",
            tableName);

        command.Parameters.AddWithValue(
            "@prefix",
            ManagedIndexPrefix + "%");

        var temporary = new Dictionary<string, (bool Unique, List<string> Columns)>(
            StringComparer.OrdinalIgnoreCase);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);

            if (!temporary.TryGetValue(
                    name,
                    out var value))
            {
                value = (
                    reader.GetBoolean(1),
                    new List<string>());

                temporary[name] = value;
            }

            value.Columns.Add(
                reader.GetString(2));
        }

        return temporary.ToDictionary(
            item => item.Key,
            item => (
                Name: item.Key,
                Unique: item.Value.Unique,
                Columns: (IReadOnlyList<string>)item.Value.Columns),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task ExecuteSchemaChange(
        SqlConnection connection,
        string modelName,
        string operation,
        string target,
        string sql,
        CancellationToken cancellationToken)
    {
        var logId = Guid.NewGuid();

        await WriteMigrationLog(
            connection,
            logId,
            modelName,
            operation,
            target,
            sql,
            "Started",
            null,
            completedAt: null,
            cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await command.ExecuteNonQueryAsync(
                cancellationToken);

            await WriteMigrationLog(
                connection,
                logId,
                modelName,
                operation,
                target,
                sql,
                "Completed",
                null,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await WriteMigrationLog(
                    connection,
                    logId,
                    modelName,
                    operation,
                    target,
                    sql,
                    "Failed",
                    exception.ToString(),
                    DateTime.UtcNow,
                    cancellationToken);
            }
            catch
            {
                // Never hide the actual migration failure because logging also failed.
            }

            throw;
        }
    }

    private async Task EnsureMigrationLogTable(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID(N'__DBMigrationLog', N'U') IS NULL
                           BEGIN
                               CREATE TABLE [__DBMigrationLog]
                               (
                                   [Id] UNIQUEIDENTIFIER NOT NULL,
                                   [Provider] NVARCHAR(50) NOT NULL,
                                   [DatabaseName] NVARCHAR(250) NOT NULL,
                                   [ModelName] NVARCHAR(250) NOT NULL,
                                   [Operation] NVARCHAR(100) NOT NULL,
                                   [Target] NVARCHAR(500) NULL,
                                   [Command] NVARCHAR(MAX) NULL,
                                   [Status] NVARCHAR(50) NOT NULL,
                                   [Error] NVARCHAR(MAX) NULL,
                                   [StartedAt] DATETIME2 NOT NULL,
                                   [CompletedAt] DATETIME2 NULL,
                                   CONSTRAINT [PK___DBMigrationLog]
                                       PRIMARY KEY ([Id])
                               );
                           END;
                           """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private async Task WriteMigrationLog(
        SqlConnection connection,
        Guid id,
        string modelName,
        string operation,
        string target,
        string commandText,
        string status,
        string? error,
        DateTime? completedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
                               IF EXISTS
                               (
                                   SELECT 1
                                   FROM [{MigrationLogTable}]
                                   WHERE [Id] = @id
                               )
                               BEGIN
                                   UPDATE [{MigrationLogTable}]
                                   SET
                                       [Status] = @status,
                                       [Error] = @error,
                                       [CompletedAt] = @completedAt
                                   WHERE [Id] = @id;
                               END
                               ELSE
                               BEGIN
                                   INSERT INTO [{MigrationLogTable}]
                                   (
                                       [Id],
                                       [Provider],
                                       [DatabaseName],
                                       [ModelName],
                                       [Operation],
                                       [Target],
                                       [Command],
                                       [Status],
                                       [Error],
                                       [StartedAt],
                                       [CompletedAt]
                                   )
                                   VALUES
                                   (
                                       @id,
                                       @provider,
                                       @databaseName,
                                       @modelName,
                                       @operation,
                                       @target,
                                       @command,
                                       @status,
                                       @error,
                                       @startedAt,
                                       @completedAt
                                   );
                               END;
                               """;

        command.Parameters.AddWithValue(
            "@id",
            id);

        command.Parameters.AddWithValue(
            "@provider",
            "SqlServer");

        command.Parameters.AddWithValue(
            "@databaseName",
            _options.Connection.Database);

        command.Parameters.AddWithValue(
            "@modelName",
            modelName);

        command.Parameters.AddWithValue(
            "@operation",
            operation);

        command.Parameters.AddWithValue(
            "@target",
            target);

        command.Parameters.AddWithValue(
            "@command",
            commandText);

        command.Parameters.AddWithValue(
            "@status",
            status);

        command.Parameters.AddWithValue(
            "@error",
            (object?)error ?? DBNull.Value);

        command.Parameters.AddWithValue(
            "@startedAt",
            DateTime.UtcNow);

        command.Parameters.AddWithValue(
            "@completedAt",
            (object?)completedAt ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
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

        foreach (var option in connection.Options)
        {
            builder[option.Key] = option.Value;
        }

        return new SqlConnection(
            builder.ConnectionString);
    }

    private static (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable) CreateExpectedColumn(
        DBColumnMetadata column)
    {
        var type = GetSqlTypeDefinition(column);

        return (
            column.ColumnName,
            type.TypeName,
            type.Length,
            type.Precision,
            type.Scale,
            IsNullable(column));
    }

    private static bool ColumnsMatch(
        (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable) existing,
        (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable) expected)
    {
        return existing.TypeName.Equals(
                   expected.TypeName,
                   StringComparison.OrdinalIgnoreCase)
               && existing.Length == expected.Length
               && existing.Precision == expected.Precision
               && existing.Scale == expected.Scale
               && existing.Nullable == expected.Nullable;
    }

    private static bool IsDestructiveChange(
        (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable) existing,
        (string Name, string TypeName, int Length, byte Precision, byte Scale, bool Nullable) expected)
    {
        if (!existing.TypeName.Equals(
                expected.TypeName,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (existing.Nullable && !expected.Nullable)
        {
            return true;
        }

        if (existing.TypeName.Equals(
                "nvarchar",
                StringComparison.OrdinalIgnoreCase))
        {
            if (existing.Length == -1)
            {
                return expected.Length != -1;
            }

            if (expected.Length == -1)
            {
                return false;
            }

            return expected.Length < existing.Length;
        }

        if (existing.TypeName.Equals(
                "decimal",
                StringComparison.OrdinalIgnoreCase))
        {
            return expected.Precision < existing.Precision
                   || expected.Scale < existing.Scale;
        }

        return false;
    }

    private static string GetSqlType(
        DBColumnMetadata column)
    {
        var definition = GetSqlTypeDefinition(column);

        return definition.TypeName switch
        {
            "nvarchar" => definition.Length == -1
                ? "NVARCHAR(MAX)"
                : $"NVARCHAR({definition.Length})",

            "decimal" =>
                $"DECIMAL({definition.Precision},{definition.Scale})",

            "varbinary" => definition.Length == -1
                ? "VARBINARY(MAX)"
                : $"VARBINARY({definition.Length})",

            _ => definition.TypeName.ToUpperInvariant()
        };
    }

    private static (string TypeName, int Length, byte Precision, byte Scale) GetSqlTypeDefinition(
        DBColumnMetadata column)
    {
        var type = column.UnderlyingType;

        if (column.IsEnum)
        {
            return column.EnumStorage == EnumStorage.Number
                ? ("int", 4, (byte)10, (byte)0)
                : ("nvarchar", NormalizeStringLength(column.Size), (byte)0, (byte)0);
        }

        if (type == typeof(string))
        {
            return ("nvarchar", NormalizeStringLength(column.Size), (byte)0, (byte)0);
        }

        if (type == typeof(int))
        {
            return ("int", 4, (byte)10, (byte)0);
        }

        if (type == typeof(long))
        {
            return ("bigint", 8, (byte)19, (byte)0);
        }

        if (type == typeof(short))
        {
            return ("smallint", 2, (byte)5, (byte)0);
        }

        if (type == typeof(byte))
        {
            return ("tinyint", 1, (byte)3, (byte)0);
        }

        if (type == typeof(bool))
        {
            return ("bit", 1, (byte)1, (byte)0);
        }

        if (type == typeof(decimal))
        {
            return ("decimal", 17, (byte)18, (byte)4);
        }

        if (type == typeof(double))
        {
            return ("float", 8, (byte)53, (byte)0);
        }

        if (type == typeof(float))
        {
            return ("real", 4, (byte)24, (byte)0);
        }

        if (type == typeof(Guid))
        {
            return ("uniqueidentifier", 16, (byte)0, (byte)0);
        }

        if (type == typeof(DateOnly))
        {
            return ("date", 3, (byte)10, (byte)0);
        }

        if (type == typeof(DateTime))
        {
            return ("datetime2", 8, (byte)27, (byte)7);
        }

        if (type == typeof(DateTimeOffset))
        {
            return ("datetimeoffset", 10, (byte)34, (byte)7);
        }

        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
        {
            return ("time", 5, (byte)16, (byte)7);
        }

        if (type == typeof(byte[]))
        {
            return ("varbinary", -1, (byte)0, (byte)0);
        }

        throw new NotSupportedException(
            $"CLR type '{type.FullName}' is not supported by the SQL Server schema synchronizer.");
    }

    private static bool IsNullable(
        DBColumnMetadata column)
    {
        // Code is the universal record identifier and primary key.
        return !column.IsCode && column.Nullable;
    }

    private static int NormalizeStringLength(
        int? size)
    {
        if (size == -1)
        {
            return -1;
        }

        return size is > 0
            ? size.Value
            : 50;
    }

    private static string BuildPrimaryKeyName(
        string tableName)
    {
        return $"PK_{NormalizeName(tableName)}_Code";
    }

    private static string BuildUniqueIndexName(
        string tableName,
        string columnName)
    {
        return $"{ManagedIndexPrefix}UQ_{NormalizeName(tableName)}_{NormalizeName(columnName)}";
    }

    private static string BuildCompositeUniqueIndexName(
        string tableName,
        string groupName)
    {
        return $"{ManagedIndexPrefix}UQ_{NormalizeName(tableName)}_{NormalizeName(groupName)}";
    }

    private static string NormalizeName(
        string value)
    {
        var invalid = value
            .Where(character => !char.IsLetterOrDigit(character) && character != '_')
            .Distinct()
            .ToArray();

        var result = value;

        foreach (var character in invalid)
        {
            result = result.Replace(
                character,
                '_');
        }

        return result;
    }

    private static string EscapeIdentifier(
        string identifier)
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
