using System.Globalization;
using System.Text.Json;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;
using SimpleORM.Net.Services;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Sample database-backed extension service.
///
/// This implementation demonstrates how an extendable DBModel can:
/// - Load published extension definitions.
/// - Load record-specific extension values.
/// - Populate DBModel.Extended.
/// - Validate extension values.
/// - Insert new extension values.
/// - Update existing extension values.
/// - Save extension values inside the same transaction as the parent model.
/// </summary>
public sealed class SampleExtensionService : IExtensionService
{
    private readonly IDatabaseProvider _provider;
    private readonly IDBMetadataProvider _metadata;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserProvider _userProvider;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SampleExtensionService"/> class.
    /// </summary>
    /// <param name="provider">
    /// Current configured database provider.
    /// </param>
    /// <param name="metadata">
    /// Model metadata provider.
    /// </param>
    /// <param name="codeGenerator">
    /// ORM code generator.
    /// </param>
    /// <param name="tenantProvider">
    /// Current tenant provider.
    /// </param>
    /// <param name="userProvider">
    /// Current user provider.
    /// </param>
    /// <param name="options">
    /// SimpleORM configuration.
    /// </param>
    public SampleExtensionService(
        IDatabaseProvider provider,
        IDBMetadataProvider metadata,
        ICodeGenerator codeGenerator,
        ITenantProvider tenantProvider,
        IUserProvider userProvider,
        SimpleOrmOptions options)
    {
        _provider = provider;
        _metadata = metadata;
        _codeGenerator = codeGenerator;
        _tenantProvider = tenantProvider;
        _userProvider = userProvider;
        _options = options;
    }

    /// <inheritdoc />
    public async Task Load<T>(
        IReadOnlyList<T> models,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        if (models.Count == 0)
        {
            return;
        }

        var metadata = _metadata.GetMetadata<T>();

        if (!metadata.Extendable)
        {
            return;
        }

        var definitions = await LoadDefinitions<T>(
            cancellationToken);

        if (definitions.Count == 0)
        {
            foreach (var model in models)
            {
                model.Extended.Clear();
            }

            return;
        }

        var modelCodes = models
            .Where(
                model =>
                    !string.IsNullOrWhiteSpace(model.Code))
            .Select(
                model => model.Code)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<DBExtensionRecordValue> storedValues;

        if (modelCodes.Length == 0)
        {
            storedValues =
                Array.Empty<DBExtensionRecordValue>();
        }
        else
        {
            storedValues =
                await LoadRecordValues<T>(
                    modelCodes,
                    cancellationToken);
        }

        var valuesByRecord = storedValues
            .GroupBy(
                value => value.ModelCode,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    value => value.FieldCode,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            model.Extended.Clear();

            Dictionary<string, DBExtensionRecordValue>?
                recordValues = null;

            if (!string.IsNullOrWhiteSpace(model.Code))
            {
                valuesByRecord.TryGetValue(
                    model.Code,
                    out recordValues);
            }

            foreach (var definition in definitions)
            {
                DBExtensionRecordValue? storedValue = null;

                if (recordValues is not null)
                {
                    recordValues.TryGetValue(
                        definition.FieldCode,
                        out storedValue);
                }

                var rawValue =
                    storedValue?.Value
                    ?? definition.DefaultValue;

                var extension = new ExtensionValue
                {
                    Code = definition.FieldCode,
                    Name = definition.FieldName,
                    DataType = definition.DataType,
                    Required = definition.Required,
                    Size = definition.Size,

                    Data = DeserializeValue(
                        rawValue,
                        definition.DataType)
                };

                model.Extended[
                    definition.FieldCode] = extension;
            }
        }
    }

    /// <inheritdoc />
    public async Task Save<T>(
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        if (models.Count == 0)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(transaction);

        var metadata = _metadata.GetMetadata<T>();

        if (!metadata.Extendable)
        {
            return;
        }

        var definitions = await LoadDefinitions<T>(
            cancellationToken);

        if (definitions.Count == 0)
        {
            return;
        }

        var definitionsByCode = definitions
            .ToDictionary(
                definition => definition.FieldCode,
                StringComparer.OrdinalIgnoreCase);

        ValidateExtensions(
            models,
            definitionsByCode);

        var modelCodes = models
            .Where(
                model =>
                    !string.IsNullOrWhiteSpace(model.Code))
            .Select(
                model => model.Code)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<DBExtensionRecordValue> existingValues;

        if (modelCodes.Length == 0)
        {
            existingValues =
                Array.Empty<DBExtensionRecordValue>();
        }
        else
        {
            existingValues =
                await LoadRecordValues<T>(
                    modelCodes,
                    cancellationToken);
        }

        var existingByKey = existingValues
            .ToDictionary(
                value => BuildKey(
                    value.ModelCode,
                    value.FieldCode),
                StringComparer.OrdinalIgnoreCase);

        var inserts =
            new List<DBExtensionRecordValue>();

        var updates =
            new List<DBExtensionRecordValue>();

        var currentTime = DateTime.UtcNow;

        var tenantCode =
            _options.MultiTenancy.Enabled
                ? _tenantProvider.GetTenantCode()
                : null;

        var userCode =
            _userProvider.GetUserCode();

        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                throw new InvalidOperationException(
                    $"Model '{typeof(T).Name}' must have a Code " +
                    "before extension values can be saved.");
            }

            foreach (var definition in definitions)
            {
                model.Extended.TryGetValue(
                    definition.FieldCode,
                    out var extension);

                var data = extension?.Data;

                var key = BuildKey(
                    model.Code,
                    definition.FieldCode);

                if (existingByKey.TryGetValue(
                        key,
                        out var existingValue))
                {
                    existingValue.Value =
                        SerializeValue(
                            data,
                            definition.DataType);

                    existingValue.UpdatedAt =
                        currentTime;

                    existingValue.UpdatedBy =
                        userCode;

                    existingValue.TenantCode =
                        tenantCode;

                    existingValue.DataState =
                        DataState.Changed;

                    updates.Add(
                        existingValue);

                    continue;
                }

                /*
                 * Do not create an extension-value record when
                 * there is no actual value.
                 *
                 * The extension definition/default is sufficient
                 * when loading the model.
                 */
                if (data is null)
                {
                    continue;
                }

                var newValue =
                    new DBExtensionRecordValue
                    {
                        Code =
                            _codeGenerator
                                .Generate<DBExtensionRecordValue>(),

                        ModelName =
                            typeof(T).Name,

                        ModelCode =
                            model.Code,

                        FieldCode =
                            definition.FieldCode,

                        Value =
                            SerializeValue(
                                data,
                                definition.DataType),

                        TenantCode =
                            tenantCode,

                        CreatedAt =
                            currentTime,

                        CreatedBy =
                            userCode,

                        DataState =
                            DataState.New
                    };

                inserts.Add(
                    newValue);
            }
        }

        foreach (var batch in inserts.Chunk(500))
        {
            await _provider.Insert(
                batch,
                transaction,
                cancellationToken);
        }

        foreach (var batch in updates.Chunk(500))
        {
            await _provider.Update(
                batch,
                transaction,
                cancellationToken);
        }
    }

    /// <summary>
    /// Loads extension definitions for a model.
    /// </summary>
    private async Task<
        IReadOnlyList<DBExtensionDefinition>>
        LoadDefinitions<T>(
            CancellationToken cancellationToken)
        where T : DBModel
    {
        var search = new SearchParam();

        search.Filters.Add(
            new SearchFilter
            {
                Field =
                    nameof(
                        DBExtensionDefinition.ModelName),

                Operator =
                    SearchOperator.Equal,

                Value =
                    typeof(T).Name
            });

        /*
         * When publishing is required, only definitions
         * that have been explicitly published are loaded.
         *
         * When RequirePublish is false, saved definitions
         * are expected to be published automatically by
         * the definition service.
         */
        if (_options.Extensions.RequirePublish)
        {
            search.Filters.Add(
                new SearchFilter
                {
                    Field =
                        nameof(
                            DBExtensionDefinition.Published),

                    Operator =
                        SearchOperator.Equal,

                    Value = true
                });
        }

        return await SelectAll<DBExtensionDefinition>(
            search,
            cancellationToken);
    }

    /// <summary>
    /// Loads extension values belonging to the supplied
    /// record codes.
    /// </summary>
    private async Task<
        IReadOnlyList<DBExtensionRecordValue>>
        LoadRecordValues<T>(
            IReadOnlyCollection<string> modelCodes,
            CancellationToken cancellationToken)
        where T : DBModel
    {
        if (modelCodes.Count == 0)
        {
            return Array.Empty<
                DBExtensionRecordValue>();
        }

        var search = new SearchParam();

        search.Filters.Add(
            new SearchFilter
            {
                Field =
                    nameof(
                        DBExtensionRecordValue.ModelName),

                Operator =
                    SearchOperator.Equal,

                Value =
                    typeof(T).Name
            });

        search.Filters.Add(
            new SearchFilter
            {
                Field =
                    nameof(
                        DBExtensionRecordValue.ModelCode),

                Operator =
                    SearchOperator.In,

                Value =
                    modelCodes.ToArray()
            });

        return await SelectAll<
            DBExtensionRecordValue>(
                search,
                cancellationToken);
    }

    /// <summary>
    /// Loads all records matching a SearchParam while
    /// respecting the maximum physical batch size of 500.
    /// </summary>
    private async Task<IReadOnlyList<TModel>>
        SelectAll<TModel>(
            SearchParam search,
            CancellationToken cancellationToken)
        where TModel : DBModel
    {
        var totalRecords =
            await _provider.Count<TModel>(
                search,
                cancellationToken);

        if (totalRecords == 0)
        {
            return Array.Empty<TModel>();
        }

        var results =
            new List<TModel>();

        var skip = 0;

        while (results.Count < totalRecords)
        {
            var remaining =
                totalRecords - results.Count;

            var limit =
                (int)Math.Min(
                    500L,
                    remaining);

            var batch =
                await _provider.Select<TModel>(
                    search,
                    skip,
                    limit,
                    cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            results.AddRange(
                batch);

            skip += batch.Count;

            if (batch.Count < limit)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Validates extension values before persistence.
    /// </summary>
    private static void ValidateExtensions<T>(
        IReadOnlyList<T> models,
        IReadOnlyDictionary<
            string,
            DBExtensionDefinition> definitions)
        where T : DBModel
    {
        foreach (var model in models)
        {
            /*
             * Reject extension codes that do not have an
             * active definition for this model.
             */
            foreach (var suppliedCode
                     in model.Extended.Keys)
            {
                if (!definitions.ContainsKey(
                        suppliedCode))
                {
                    throw new InvalidOperationException(
                        $"Extension '{suppliedCode}' is " +
                        $"not defined or published for " +
                        $"model '{typeof(T).Name}'.");
                }
            }

            foreach (var definition
                     in definitions.Values)
            {
                model.Extended.TryGetValue(
                    definition.FieldCode,
                    out var extension);

                var value =
                    extension?.Data
                    ?? DeserializeValue(
                        definition.DefaultValue,
                        definition.DataType);

                if (definition.Required
                    && value is null)
                {
                    throw new InvalidOperationException(
                        $"Extension " +
                        $"'{definition.FieldCode}' " +
                        $"is required for model " +
                        $"'{typeof(T).Name}'.");
                }

                if (definition.DataType
                        == ExtensionDataType.String
                    && definition.Size is > 0
                    && value?.ToString()?.Length
                        > definition.Size.Value)
                {
                    throw new InvalidOperationException(
                        $"Extension " +
                        $"'{definition.FieldCode}' " +
                        $"exceeds its maximum size of " +
                        $"{definition.Size.Value} characters.");
                }

                /*
                 * Serialization also validates that the
                 * supplied value can actually be converted
                 * to the declared extension type.
                 */
                if (value is not null)
                {
                    _ = SerializeValue(
                        value,
                        definition.DataType);
                }
            }
        }
    }

    /// <summary>
    /// Builds a unique in-memory key for an extension
    /// record value.
    /// </summary>
    private static string BuildKey(
        string modelCode,
        string fieldCode)
    {
        return string.Concat(
            modelCode,
            "\u001F",
            fieldCode);
    }

    /// <summary>
    /// Serializes an extension value for database storage.
    /// </summary>
    private static string? SerializeValue(
        object? value,
        ExtensionDataType dataType)
    {
        if (value is null)
        {
            return null;
        }

        value = NormalizeJsonValue(
            value,
            dataType);

        return dataType switch
        {
            ExtensionDataType.String =>
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.Integer =>
                Convert.ToInt32(
                        value,
                        CultureInfo.InvariantCulture)
                    .ToString(
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Long =>
                Convert.ToInt64(
                        value,
                        CultureInfo.InvariantCulture)
                    .ToString(
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Decimal =>
                Convert.ToDecimal(
                        value,
                        CultureInfo.InvariantCulture)
                    .ToString(
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Double =>
                Convert.ToDouble(
                        value,
                        CultureInfo.InvariantCulture)
                    .ToString(
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Boolean =>
                Convert.ToBoolean(
                        value,
                        CultureInfo.InvariantCulture)
                    .ToString(
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Date =>
                ConvertToDateOnly(value)
                    .ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),

            ExtensionDataType.DateTime =>
                ConvertToDateTime(value)
                    .ToUniversalTime()
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture),

            ExtensionDataType.Guid =>
                ConvertToGuid(value)
                    .ToString(),

            ExtensionDataType.Json =>
                value is string text
                    ? text
                    : JsonSerializer.Serialize(value),

            _ =>
                throw new NotSupportedException(
                    $"Extension data type " +
                    $"'{dataType}' is not supported.")
        };
    }

    /// <summary>
    /// Deserializes a persisted extension value.
    /// </summary>
    private static object? DeserializeValue(
        string? value,
        ExtensionDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return dataType switch
        {
            ExtensionDataType.String =>
                value,

            ExtensionDataType.Integer =>
                int.Parse(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.Long =>
                long.Parse(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.Decimal =>
                decimal.Parse(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.Double =>
                double.Parse(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.Boolean =>
                bool.Parse(value),

            ExtensionDataType.Date =>
                DateOnly.Parse(
                    value,
                    CultureInfo.InvariantCulture),

            ExtensionDataType.DateTime =>
                DateTime.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),

            ExtensionDataType.Guid =>
                Guid.Parse(value),

            ExtensionDataType.Json =>
                JsonSerializer.Deserialize<
                    JsonElement>(value),

            _ =>
                throw new NotSupportedException(
                    $"Extension data type " +
                    $"'{dataType}' is not supported.")
        };
    }

    /// <summary>
    /// Converts a JSON request value to the CLR value
    /// expected by the extension definition.
    /// </summary>
    private static object NormalizeJsonValue(
        object value,
        ExtensionDataType dataType)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return dataType switch
        {
            ExtensionDataType.String =>
                element.ValueKind
                    == JsonValueKind.Null
                    ? string.Empty
                    : element.GetString()
                      ?? string.Empty,

            ExtensionDataType.Integer =>
                element.GetInt32(),

            ExtensionDataType.Long =>
                element.GetInt64(),

            ExtensionDataType.Decimal =>
                element.GetDecimal(),

            ExtensionDataType.Double =>
                element.GetDouble(),

            ExtensionDataType.Boolean =>
                element.GetBoolean(),

            ExtensionDataType.Date =>
                element.GetString()
                ?? throw new InvalidOperationException(
                    "Date extension value cannot be null."),

            ExtensionDataType.DateTime =>
                element.GetString()
                ?? throw new InvalidOperationException(
                    "DateTime extension value cannot be null."),

            ExtensionDataType.Guid =>
                element.GetString()
                ?? throw new InvalidOperationException(
                    "Guid extension value cannot be null."),

            ExtensionDataType.Json =>
                element.GetRawText(),

            _ =>
                element.GetRawText()
        };
    }

    /// <summary>
    /// Converts a value to DateOnly.
    /// </summary>
    private static DateOnly ConvertToDateOnly(
        object value)
    {
        return value switch
        {
            DateOnly date =>
                date,

            DateTime dateTime =>
                DateOnly.FromDateTime(dateTime),

            _ =>
                DateOnly.Parse(
                    value.ToString()!,
                    CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Converts a value to DateTime.
    /// </summary>
    private static DateTime ConvertToDateTime(
        object value)
    {
        return value switch
        {
            DateTime dateTime =>
                dateTime,

            DateTimeOffset offset =>
                offset.UtcDateTime,

            _ =>
                DateTime.Parse(
                    value.ToString()!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)
        };
    }

    /// <summary>
    /// Converts a value to Guid.
    /// </summary>
    private static Guid ConvertToGuid(
        object value)
    {
        return value is Guid guid
            ? guid
            : Guid.Parse(
                value.ToString()!);
    }
}