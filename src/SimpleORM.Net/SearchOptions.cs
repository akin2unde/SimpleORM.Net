namespace SimpleORM.Net.Configuration;

/// <summary>Search conventions.</summary>
public sealed class SearchOptions
{

    /// <summary>Whether Tenant participates in generic search; isolation is separate.</summary>
    public bool IncludeTenantCode
    {
        get;

        set;

    }

}
