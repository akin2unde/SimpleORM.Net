using SimpleORM.Net.Attributes;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.SystemModels;

/// <summary>Extension record value.</summary>
[DisableAudit,DBTable("__DBExtensionValue"),DBCode(Prefix="EXV")]
public sealed class DBExtensionRecordValue:DBModel
{

    /// <summary>Model.</summary>
    [Unique("ModelRecordField")]
    public string ModelName
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Record code.</summary>
    [Unique("ModelRecordField")]
    public string ModelCode
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Field code.</summary>
    [Unique("ModelRecordField")]
    public string FieldCode
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Serialized value.</summary>
    [DBColumn(Size=-1)]
    public string? Value
    {
        get;

        set;

    }

}
