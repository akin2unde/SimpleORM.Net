namespace SimpleORM.Net.Attributes;

/// <summary>
/// Overrides the convention-based model code prefix and/or random suffix length.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DBCodeAttribute : Attribute
{

    private int _length;

    /// <summary>
    /// Gets or sets the code prefix. When omitted, the configured number of leading
    /// model-name characters is used.
    /// </summary>
    public string? Prefix
    {
        get;

        set;

    }

    /// <summary>
    /// Gets or sets the random suffix length for this model. When omitted, the
    /// injection-level code length is used.
    /// </summary>
    public int Length

    {

        get => _length;

        set

        {

            _length = value;

            HasLength = true;

        }

    }

    /// <summary>Indicates whether <see cref="Length"/> was explicitly supplied.</summary>
    internal bool HasLength
    {
        get;

        private set;

    }

}
