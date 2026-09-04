using SimpleORM.Net.Models;

namespace SimpleORM.Net.Metadata;

/// <summary>Cached model metadata.</summary>
public sealed class DBModelMetadata
{
    /// <summary>Gets the CLR model type.</summary>
    public required Type ModelType { get; init; }

    /// <summary>Gets the CLR model name.</summary>
    public required string ModelName { get; init; }

    /// <summary>Gets the database table or collection name.</summary>
    public required string TableName { get; init; }

    /// <summary>Gets the default generated-code prefix.</summary>
    public required string CodePrefix { get; init; }

    /// <summary>Gets the generated-code suffix length.</summary>
    public int CodeLength { get; init; }

    /// <summary>Gets whether the model uses hard delete.</summary>
    public bool HardDelete { get; init; }

    /// <summary>Gets whether the model supports dynamic extensions.</summary>
    public bool Extendable { get; init; }

    /// <summary>Gets whether audit trail is enabled for the model.</summary>
    public bool AuditEnabled { get; init; }

    /// <summary>Gets whether optimistic concurrency checks are enabled for the model.</summary>
    public bool ConcurrencyEnabled { get; init; }

    /// <summary>
    /// Gets whether the model participates in tenant scoping when application
    /// multi-tenancy is enabled.
    /// </summary>
    public bool TenantScoped { get; init; }

    /// <summary>Gets the stale-data retention period, when automatic deletion is enabled.</summary>
    public int? AutoDeleteAfterDays { get; init; }

    /// <summary>Gets the UTC cron expression for automatic stale-data deletion.</summary>
    public string? AutoDeleteCron { get; init; }

    /// <summary>Gets all discovered columns, including ignored properties.</summary>
    public required IReadOnlyList<DBColumnMetadata> Columns { get; init; }

    /// <summary>Gets the Code column metadata.</summary>
    public required DBColumnMetadata CodeColumn { get; init; }

    /// <summary>Gets the tenant column metadata for tenant-scoped models.</summary>
    public DBColumnMetadata? TenantColumn { get; init; }

    /// <summary>Gets columns that are persisted by the selected provider.</summary>
    public IEnumerable<DBColumnMetadata> PersistedColumns =>
        Columns.Where(column => !column.Ignore);

    /// <summary>Gets searchable persisted string columns.</summary>
    public IEnumerable<DBColumnMetadata> SearchableColumns =>
        Columns.Where(column => column.Searchable && !column.Ignore);
}
