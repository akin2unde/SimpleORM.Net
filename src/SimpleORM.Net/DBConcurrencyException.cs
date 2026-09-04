namespace SimpleORM.Net.Exceptions;

/// <summary>
/// Raised when a model was changed or removed after the caller loaded it.
/// </summary>
public sealed class DBConcurrencyException : Exception
{
    /// <summary>Creates a concurrency exception.</summary>
    public DBConcurrencyException(Type modelType, IEnumerable<string> codes)
        : base(BuildMessage(modelType, codes))
    {
        ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
        Codes = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Gets the model type involved in the conflict.</summary>
    public Type ModelType { get; }

    /// <summary>
    /// Gets the records involved in the failed concurrency-protected batch.
    /// Providers may return the complete candidate batch when the underlying bulk API
    /// cannot identify the precise conflicting row without adding a pre-read.
    /// </summary>
    public IReadOnlyList<string> Codes { get; }

    private static string BuildMessage(Type modelType, IEnumerable<string> codes)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(codes);

        var values = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        var suffix = values.Length == 0
            ? string.Empty
            : $" Records: {string.Join(", ", values)}{(values.Length == 10 ? ", ..." : string.Empty)}.";

        return $"The {modelType.Name} record has been modified or removed since it was loaded. Reload the latest data before saving.{suffix}";
    }
}
