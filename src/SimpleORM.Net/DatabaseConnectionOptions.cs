namespace SimpleORM.Net.Configuration;


/// <summary>Provider-neutral connection configuration.</summary>
public sealed class DatabaseConnectionOptions
{

    /// <summary>Host.</summary>
    public string Host
    {
        get;

        set;

    }
    ="localhost";


    /// <summary>Port.</summary>
    public int Port
    {
        get;

        set;

    }

    /// <summary>Database name.</summary>
    public string Database
    {
        get;

        set;

    }
    =string.Empty;


    /// <summary>Username.</summary>
    public string? Username
    {
        get;

        set;

    }

    /// <summary>Plain or ENC:-prefixed password.</summary>
    public string? Password
    {
        get;

        set;

    }

    /// <summary>Transport SSL.</summary>
    public bool UseSsl
    {
        get;

        set;

    }

    /// <summary>Provider-specific options.</summary>
    public IDictionary<string,string> Options
    {
        get;

    }
    =new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);


}
