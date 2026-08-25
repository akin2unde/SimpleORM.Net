using System.Collections.Concurrent;

using System.Reflection;

using SimpleORM.Net.Attributes;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Metadata;

/// <summary>Cached property metadata.</summary>
public sealed class DBColumnMetadata
{

    /// <summary>Reflection property.</summary>
    public required PropertyInfo Property
    {
        get;

        init;

    }

    /// <summary>CLR name.</summary>
    public required string PropertyName
    {
        get;

        init;

    }

    /// <summary>DB name.</summary>
    public required string ColumnName
    {
        get;

        init;

    }

    /// <summary>CLR type.</summary>
    public required Type PropertyType
    {
        get;

        init;

    }

    /// <summary>Unwrapped type.</summary>
    public required Type UnderlyingType
    {
        get;

        init;

    }

    /// <summary>Size.</summary>
    public int? Size
    {
        get;

        init;

    }

    /// <summary>Nullable.</summary>
    public bool Nullable
    {
        get;

        init;

    }

    /// <summary>Unique.</summary>
    public bool Unique
    {
        get;

        init;

    }

    /// <summary>Composite unique group.</summary>
    public string? UniqueGroup
    {
        get;

        init;

    }

    /// <summary>Ignored.</summary>
    public bool Ignore
    {
        get;

        init;

    }

    /// <summary>Reset before return.</summary>
    public bool DefaultOnReturn
    {
        get;

        init;

    }

    /// <summary>Omit from audit.</summary>
    public bool DoNotAudit
    {
        get;

        init;

    }

    /// <summary>Generic-search eligible.</summary>
    public bool Searchable
    {
        get;

        init;

    }

    /// <summary>Enum.</summary>
    public bool IsEnum
    {
        get;

        init;

    }

    /// <summary>Enum storage.</summary>
    public EnumStorage? EnumStorage
    {
        get;

        init;

    }

    /// <summary>Code property.</summary>
    public bool IsCode
    {
        get;

        init;

    }

    /// <summary>Tenant property.</summary>
    public bool IsTenantCode
    {
        get;

        init;

    }

}
