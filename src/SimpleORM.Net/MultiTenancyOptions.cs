namespace SimpleORM.Net.Configuration;


/// <summary>Tenant conventions.</summary>
public sealed class MultiTenancyOptions
{

    /// <summary>Enable tenant isolation.</summary>
    public bool Enabled
    {
        get;

        set;

    }

    /// <summary>JWT tenant claim.</summary>
    public string JwtClaim
    {
        get;

        set;

    }
    ="tenant";


}
