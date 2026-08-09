using SimpleORM.Net.Attributes;


using SimpleORM.Net.Models;


namespace SimpleORM.Net.SystemModels;


/// <summary>Audit operation.</summary>
public enum AuditAction
{

    /// <summary>Insert.</summary>
    Insert,
    /// <summary>Update.</summary>
    Update,
    /// <summary>Delete.</summary>
    Delete
}
