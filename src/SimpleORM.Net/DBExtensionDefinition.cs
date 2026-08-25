using SimpleORM.Net.Attributes;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.SystemModels;

/// <summary>Extension definition.</summary>
[DisableAudit,DBTable("__DBExtensionDefinition"),DBCode(Prefix="EXT")]
public sealed class DBExtensionDefinition:DBModel
{

    /// <summary>Model name.</summary>
    [Unique("ModelField")]
    public string ModelName
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Field code.</summary>
    [Unique("ModelField")]
    public string FieldCode
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Friendly name.</summary>
    public string FieldName
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Data type.</summary>
    public ExtensionDataType DataType
    {
        get;

        set;

    }

    /// <summary>Required.</summary>
    public bool Required
    {
        get;

        set;

    }

    /// <summary>Size.</summary>
    public int? Size
    {
        get;

        set;

    }

    /// <summary>Default serialized value.</summary>
    public string? DefaultValue
    {
        get;

        set;

    }

    /// <summary>Published.</summary>
    public bool Published
    {
        get;

        set;

    }

}
