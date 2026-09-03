using Cronos;
using System.Collections.Concurrent;
using System.Reflection;
using SimpleORM.Net.Attributes;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Metadata;

/// <summary>Reflection-once application-lifetime metadata cache.</summary>
public sealed class DBMetadataProvider : IDBMetadataProvider
{
    private readonly SimpleOrmOptions _options;
    private readonly ConcurrentDictionary<Type, DBModelMetadata> _cache = new();

    /// <summary>Initializes the metadata provider.</summary>
    /// <param name="options">SimpleORM configuration.</param>
    public DBMetadataProvider(SimpleOrmOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public DBModelMetadata GetMetadata<T>()
        where T : DBModel
    {
        return GetMetadata(typeof(T));
    }

    /// <inheritdoc />
    public DBModelMetadata GetMetadata(Type type)
    {
        if (!typeof(DBModel).IsAssignableFrom(type) || type.IsAbstract)
        {
            throw new InvalidOperationException(
                $"{type.FullName} must be a concrete DBModel.");
        }

        return _cache.GetOrAdd(type, Build);
    }

    /// <inheritdoc />
    public void RegisterModel(Type type)
    {
        _ = GetMetadata(type);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DBModelMetadata> GetRegisteredModels()
    {
        return _cache.Values.ToArray();
    }

    private DBModelMetadata Build(Type type)
    {
        var codeAttribute = type.GetCustomAttribute<DBCodeAttribute>(true);
        var autoDeleteAttribute = type.GetCustomAttribute<AutoDeleteAttribute>(true);
        var tenantScoped = !type.IsDefined(typeof(GlobalAttribute), true);

        if (autoDeleteAttribute is not null)
        {
            try
            {
                _ = CronExpression.Parse(
                    autoDeleteAttribute.Cron,
                    CronFormat.Standard);
            }
            catch (CronFormatException exception)
            {
                throw new InvalidOperationException(
                    $"Auto-delete cron for model '{type.Name}' is invalid.",
                    exception);
            }
        }

        var prefixLength = Math.Min(
            Math.Max(1, _options.CodeGeneration.PrefixLength),
            type.Name.Length);

        var prefix = string.IsNullOrWhiteSpace(codeAttribute?.Prefix)
            ? type.Name[..prefixLength].ToUpperInvariant()
            : codeAttribute!.Prefix!.Trim().ToUpperInvariant();

        var codeLength = codeAttribute is { HasLength: true }
            ? codeAttribute.Length
            : _options.CodeGeneration.Length;

        if (codeLength < 4)
        {
            throw new InvalidOperationException(
                $"Code length for {type.Name} must be at least 4.");
        }

        var columns = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => BuildColumn(property, tenantScoped))
            .ToArray();

        return new DBModelMetadata
        {
            ModelType = type,
            ModelName = type.Name,
            TableName = type.GetCustomAttribute<DBTableAttribute>(true)?.Name
                ?? type.Name,
            CodePrefix = prefix,
            CodeLength = codeLength,
            HardDelete = type.IsDefined(typeof(HardDeleteAttribute), true),
            Extendable = type.IsDefined(typeof(ExtendableAttribute), true),
            AuditEnabled = !type.IsDefined(typeof(DisableAuditAttribute), true),
            TenantScoped = tenantScoped,
            AutoDeleteAfterDays = autoDeleteAttribute?.OlderThanDays,
            AutoDeleteCron = autoDeleteAttribute?.Cron,
            Columns = columns,
            CodeColumn = columns.Single(
                column => column.PropertyName == nameof(DBModel.Code)),
            TenantColumn = tenantScoped
                ? columns.SingleOrDefault(
                    column =>
                        column.PropertyName == nameof(DBModel.Tenant)
                        && !column.Ignore)
                : null
        };
    }

    private DBColumnMetadata BuildColumn(
        PropertyInfo property,
        bool tenantScoped)
    {
        var attribute = property.GetCustomAttribute<DBColumnAttribute>(true);
        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType)
            ?? propertyType;
        var isTenantColumn = property.Name == nameof(DBModel.Tenant);
        var explicitlyIgnored = property.IsDefined(typeof(IgnoreAttribute), true);
        var ignored = explicitlyIgnored || (isTenantColumn && !tenantScoped);
        var isEnum = underlyingType.IsEnum;

        EnumStorage? enumStorage = null;

        if (isEnum)
        {
            enumStorage = attribute is { HasEnumStorage: true }
                ? attribute.EnumStorage
                : _options.EnumStorage;
        }

        int? size = null;

        if (underlyingType == typeof(string)
            || (isEnum && enumStorage == EnumStorage.String))
        {
            size = attribute is { HasSize: true }
                ? attribute.Size
                : _options.DefaultStringLength;
        }

        var uniqueAttribute = property
            .GetCustomAttributes<UniqueAttribute>(true)
            .FirstOrDefault();

        var defaultOnReturn = property.IsDefined(
            typeof(DefaultOnReturnAttribute),
            true);

        return new DBColumnMetadata
        {
            Property = property,
            PropertyName = property.Name,
            ColumnName = string.IsNullOrWhiteSpace(attribute?.Name)
                ? property.Name
                : attribute!.Name!,
            PropertyType = propertyType,
            UnderlyingType = underlyingType,
            Size = size,
            Nullable = attribute is { HasNullable: true }
                ? attribute.Nullable
                : Nullable.GetUnderlyingType(propertyType) is not null
                    || !propertyType.IsValueType,
            Unique = property.Name == nameof(DBModel.Code)
                || uniqueAttribute is not null,
            UniqueGroup = uniqueAttribute?.Group,
            Ignore = ignored,
            DefaultOnReturn = defaultOnReturn,
            DoNotAudit = ignored
                || defaultOnReturn
                || property.IsDefined(typeof(DoNotAuditAttribute), true),
            Searchable = underlyingType == typeof(string)
                && !ignored
                && !defaultOnReturn
                && !property.IsDefined(typeof(NotSearchableAttribute), true)
                && (!isTenantColumn || _options.Search.IncludeTenantCode),
            IsEnum = isEnum,
            EnumStorage = enumStorage,
            IsCode = property.Name == nameof(DBModel.Code),
            IsTenantCode = isTenantColumn
        };
    }
}
