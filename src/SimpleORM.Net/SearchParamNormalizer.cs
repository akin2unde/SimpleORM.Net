using System.Collections;
using System.Globalization;
using System.Text.Json;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Query;

/// <summary>
/// Default provider-neutral search value normalizer.
/// </summary>
public sealed class SearchParamNormalizer(
    IDBMetadataProvider metadata) : ISearchParamNormalizer
{
    /// <inheritdoc />
    public SearchParam Normalize<T>(SearchParam? search)
        where T : DBModel
    {
        var normalized = search?.Clone() ?? new SearchParam();
        var modelMetadata = metadata.GetMetadata<T>();

        foreach (var filter in normalized.Filters)
        {
            if (filter.Operator is SearchOperator.IsNull or SearchOperator.IsNotNull)
            {
                filter.Value = null;
                continue;
            }

            var column = modelMetadata.Columns.FirstOrDefault(candidate =>
                candidate.PropertyName.Equals(filter.Field, StringComparison.OrdinalIgnoreCase)
                || candidate.ColumnName.Equals(filter.Field, StringComparison.OrdinalIgnoreCase));

            if (column is null)
            {
                throw new InvalidOperationException(
                    $"Property or column '{filter.Field}' was not found on model '{modelMetadata.ModelName}'.");
            }

            var valueType = GetFilterValueType(column.PropertyType, filter.Operator);
            filter.Value = IsMultiValueOperator(filter.Operator)
                ? ConvertMany(filter.Value, valueType, filter.Operator)
                : ConvertValue(filter.Value, valueType);
        }

        return normalized;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        value = UnwrapJsonElement(value);

        if (value is null)
        {
            return null;
        }

        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            return ConvertEnum(value, targetType);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(
                System.Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(DateOnly))
        {
            return DateOnly.Parse(
                System.Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(TimeOnly))
        {
            return TimeOnly.Parse(
                System.Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(string))
        {
            return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static object ConvertEnum(object value, Type enumType)
    {
        if (value is string text)
        {
            if (Enum.TryParse(enumType, text, true, out var parsed))
            {
                return parsed!;
            }

            throw new ArgumentException(
                $"'{text}' is not a valid value for enum '{enumType.Name}'.");
        }

        var underlyingType = Enum.GetUnderlyingType(enumType);
        var numericValue = System.Convert.ChangeType(
            value,
            underlyingType,
            CultureInfo.InvariantCulture);

        return Enum.ToObject(enumType, numericValue!);
    }

    private static List<object?> ConvertMany(
        object? value,
        Type valueType,
        SearchOperator searchOperator)
    {
        value = UnwrapJsonElement(value);

        if (value is string || value is not IEnumerable enumerable)
        {
            throw new ArgumentException(
                $"Search operator '{searchOperator}' requires an enumerable value.");
        }

        return enumerable
            .Cast<object?>()
            .Select(item => ConvertValue(item, valueType))
            .ToList();
    }

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integerValue) => integerValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => element
                .EnumerateArray()
                .Select(item => UnwrapJsonElement(item))
                .ToList(),
            _ => element.GetRawText()
        };
    }

    private static Type GetFilterValueType(
        Type propertyType,
        SearchOperator searchOperator)
    {
        var actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (!IsMultiValueOperator(searchOperator) || actualType == typeof(string))
        {
            return actualType;
        }

        if (actualType.IsArray)
        {
            return actualType.GetElementType()!;
        }

        return actualType.IsGenericType
            ? actualType.GetGenericArguments()[0]
            : actualType;
    }

    private static bool IsMultiValueOperator(SearchOperator searchOperator) =>
        searchOperator is SearchOperator.In
            or SearchOperator.NotIn
            or SearchOperator.Between
            or SearchOperator.NotBetween;
}
