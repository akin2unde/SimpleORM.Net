using System.Collections.Concurrent;

using System.Reflection;

using SimpleORM.Net.Attributes;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Metadata;

/// <summary>Cached model metadata.</summary>
public sealed class DBModelMetadata
{

    /// <summary>Type.</summary>
    public required Type ModelType
    {
        get;

        init;

    }

    /// <summary>Name.</summary>
    public required string ModelName
    {
        get;

        init;

    }

    /// <summary>Table/collection.</summary>
    public required string TableName
    {
        get;

        init;

    }

    /// <summary>Code prefix.</summary>
    public required string CodePrefix
    {
        get;

        init;

    }

    /// <summary>Code suffix length.</summary>
    public int CodeLength
    {
        get;

        init;

    }

    /// <summary>Hard delete.</summary>
    public bool HardDelete
    {
        get;

        init;

    }

    /// <summary>Extendable.</summary>
    public bool Extendable
    {
        get;

        init;

    }

    /// <summary>Audit enabled.</summary>
    public bool AuditEnabled
    {
        get;

        init;

    }

    /// <summary>Columns.</summary>
    public required IReadOnlyList<DBColumnMetadata> Columns
    {
        get;

        init;

    }

    /// <summary>Code column.</summary>
    public required DBColumnMetadata CodeColumn
    {
        get;

        init;

    }

    /// <summary>Tenant column.</summary>
    public DBColumnMetadata? TenantColumn
    {
        get;

        init;

    }

    /// <summary>Persisted columns.</summary>
    public IEnumerable<DBColumnMetadata> PersistedColumns=>Columns.Where(x=>!x.Ignore);

    /// <summary>Searchable columns.</summary>
    public IEnumerable<DBColumnMetadata> SearchableColumns=>Columns.Where(x=>x.Searchable&&!x.Ignore);

}
