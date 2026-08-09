namespace SimpleORM.Net.Configuration;


/// <summary>Audit configuration.</summary>
public sealed class AuditTrailOptions
{

    /// <summary>Enable audit.</summary>
    public bool Enabled
    {
        get;

        set;

    }

    /// <summary>Old values.</summary>
    public bool IncludeOldValues
    {
        get;

        set;

    }
    =true;


    /// <summary>New values.</summary>
    public bool IncludeNewValues
    {
        get;

        set;

    }
    =true;


}
