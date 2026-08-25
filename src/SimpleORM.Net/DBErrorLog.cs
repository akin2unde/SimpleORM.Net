using SimpleORM.Net.Attributes;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.SystemModels;

/// <summary>Error log record.</summary>
[DisableAudit,DBTable("__DBErrorLog"),DBCode(Prefix="DBE")]
public sealed class DBErrorLog:DBModel
{

    /// <summary>User.</summary>
    public string? UserCode
    {
        get;

        set;

    }

    /// <summary>URL.</summary>
    public string? RequestUrl
    {
        get;

        set;

    }

    /// <summary>HTTP method.</summary>
    public string? HttpMethod
    {
        get;

        set;

    }

    /// <summary>Caller IP.</summary>
    public string? CallerIp
    {
        get;

        set;

    }

    /// <summary>User agent.</summary>
    [DBColumn(Size=500)]
    public string? UserAgent
    {
        get;

        set;

    }

    /// <summary>Trace.</summary>
    public string? TraceCode
    {
        get;

        set;

    }

    /// <summary>Model.</summary>
    public string? ModelName
    {
        get;

        set;

    }

    /// <summary>Operation.</summary>
    public string? Operation
    {
        get;

        set;

    }

    /// <summary>Transaction failure.</summary>
    public bool TransactionFailed
    {
        get;

        set;

    }

    /// <summary>Sanitized payload.</summary>
    [DBColumn(Size=-1)]
    public string? Payload
    {
        get;

        set;

    }

    /// <summary>Exception type.</summary>
    public string ExceptionType
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Message.</summary>
    [DBColumn(Size=-1)]
    public string Message
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Stack.</summary>
    [DBColumn(Size=-1)]
    public string? StackTrace
    {
        get;

        set;

    }

    /// <summary>Inner exception.</summary>
    [DBColumn(Size=-1)]
    public string? InnerException
    {
        get;

        set;

    }

}
