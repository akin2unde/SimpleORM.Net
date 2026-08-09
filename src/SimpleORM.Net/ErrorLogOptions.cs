namespace SimpleORM.Net.Configuration;


/// <summary>Error-log configuration.</summary>
public sealed class ErrorLogOptions
{

    /// <summary>Enable DB error log.</summary>
    public bool Enabled
    {
        get;

        set;

    }

    /// <summary>Capture JSON payload.</summary>
    public bool IncludePayload
    {
        get;

        set;

    }
    =true;


    /// <summary>Payload max chars.</summary>
    public int MaxPayloadLength
    {
        get;

        set;

    }
    =10_000;


}
