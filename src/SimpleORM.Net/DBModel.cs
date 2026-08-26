using System.Security.Cryptography;
using SimpleORM.Net.Attributes;

namespace SimpleORM.Net.Models;

/// <summary>Base type for every persisted model.</summary>
public abstract class DBModel
{
    /// <summary>Globally unique business code.</summary>
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Returns the prefix used when generating a code for this model.</summary>
    public virtual string GetPrefix()
    {
        var name = GetType().Name;
        return name[..Math.Min(3, name.Length)].ToUpperInvariant();
    }

    /// <summary>Generates a code for this model instance.</summary>
    public virtual string GenerateCode(int length = 10, string separator = "-")
    {
        if (length < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Code length must be at least 4.");
        }

        return $"{GetPrefix()}{separator}{RandomNumberGenerator.GetString(CodeAlphabet, length)}";
    }

    /// <summary>Globally unique business code.</summary>
    public virtual string Code
    {
        get;

        set;

    }
    = string.Empty;

    /// <summary>Current persistence instruction.</summary>
    [Ignore]
    public virtual DataState DataState
    {
        get;

        set;

    }
    = DataState.New;

    /// <summary>Set upsert.</summary>
    [Ignore]
    public bool Upsert
    {
        get;

        set;

    }
    = false;

    /// <summary>Tenant code.</summary>
    public virtual string? Tenant
    {
        get;

        set;

    }

    /// <summary>UTC creation time.</summary>
    public virtual DateTime CreatedAt
    {
        get;

        set;

    }

    /// <summary>UTC last update.</summary>
    public virtual DateTime? UpdatedAt
    {
        get;

        set;

    }

    /// <summary>UTC soft-delete time.</summary>
    public virtual DateTime? DeletedAt
    {
        get;

        set;

    }

    /// <summary>Creator code.</summary>
    public virtual string? CreatedBy
    {
        get;

        set;

    }

    /// <summary>Last updater code.</summary>
    public virtual string? UpdatedBy
    {
        get;

        set;

    }

    /// <summary>Dynamic extensions; never stored in the base table/collection.</summary>
    [Ignore]
    public virtual IDictionary<string, ExtensionValue> Extended
    {
        get;

        set;

    }
    = new Dictionary<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase);

}
