using SimpleORM.Net.Attributes;


namespace SimpleORM.Net.Models;


/// <summary>Dynamic extension definition plus a record value.</summary>
public sealed class ExtensionValue
{

    /// <summary>Field code.</summary>
    public string Code
    {
        get;

        set;


    }
    = string.Empty;


    /// <summary>Friendly name.</summary>
    public string Name
    {
        get;

        set;


    }
    = string.Empty;


    /// <summary>Declared type.</summary>
    public ExtensionDataType DataType
    {
        get;

        set;


    }

    /// <summary>Required flag.</summary>
    public bool Required
    {
        get;

        set;


    }

    /// <summary>Optional max size.</summary>
    public int? Size
    {
        get;

        set;


    }

    /// <summary>Current record value.</summary>
    public object? Data
    {
        get;

        set;


    }

}
