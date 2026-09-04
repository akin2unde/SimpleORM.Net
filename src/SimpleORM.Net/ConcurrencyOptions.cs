namespace SimpleORM.Net.Configuration;

/// <summary>Optimistic concurrency settings.</summary>
public sealed class ConcurrencyOptions
{
    /// <summary>
    /// Gets or sets whether optimistic concurrency checks are enabled by default.
    /// Models can opt out with <see cref="SimpleORM.Net.Attributes.DisableConcurrencyCheckAttribute"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
