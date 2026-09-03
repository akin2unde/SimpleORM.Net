using SimpleORM.Net.Attributes;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Demonstrates a shared model that does not belong to any tenant.
/// </summary>
[Global]
public sealed class Country : DBModel
{
    /// <summary>Gets or sets the country name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a transient display value that is never persisted.
    /// </summary>
    [Ignore]
    public string? DisplayLabel { get; set; }
}
