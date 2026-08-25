using SimpleORM.Net.Attributes;

namespace SimpleORM.Net.Models;

/// <summary>Persistence state used by the single Save API.</summary>
public enum DataState
{

    /// <summary>No pending change.</summary>
    Unchanged,
    /// <summary>Insert.</summary>
    New,
    /// <summary>Update.</summary>
    Changed,
    /// <summary>Soft/hard delete.</summary>
    Removed
}
