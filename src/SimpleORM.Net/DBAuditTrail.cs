using SimpleORM.Net.Attributes;


using SimpleORM.Net.Models;


namespace SimpleORM.Net.SystemModels;


/// <summary>Audit trail record.</summary>
[DisableAudit,DBTable("__DBAuditTrail"),DBCode(Prefix="AUD")]
public sealed class DBAuditTrail:DBModel
{

    /// <summary>Model.</summary>
    public string ModelName
    {
        get;

        set;

    }
    =string.Empty;


    /// <summary>Record code.</summary>
    public string RecordCode
    {
        get;

        set;

    }
    =string.Empty;


    /// <summary>Action.</summary>
    public AuditAction Action
    {
        get;

        set;

    }

    /// <summary>Old JSON.</summary>
    [DBColumn(Size=-1)]
    public string? OldData
    {
        get;

        set;

    }

    /// <summary>New JSON.</summary>
    [DBColumn(Size=-1)]
    public string? NewData
    {
        get;

        set;

    }

    /// <summary>User.</summary>
    public string? UserCode
    {
        get;

        set;

    }

}
