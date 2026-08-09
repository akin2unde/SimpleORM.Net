using SimpleORM.Net.Attributes;


namespace SimpleORM.Net.Models;


/// <summary>Base type for every persisted model.</summary>
public abstract class DBModel
{

    /// <summary>Globally unique business code.</summary>
    public virtual string Code
    {
        get;

        set;


    }
    = string.Empty;


    /// <summary>Current persistence instruction.</summary>
    [Ignore]
    public virtual DataState DataState
    {
        get;

        set;


    }
    = DataState.New;


    /// <summary>Tenant code.</summary>
    public virtual string? TenantCode
    {
        get;

        set;


    }

    /// <summary>UTC creation time.</summary>
    public virtual DateTime CreatedAt
    {
        get;

        set;


    }

    /// <summary>UTC last update.</summary>
    public virtual DateTime? UpdatedAt
    {
        get;

        set;


    }

    /// <summary>UTC soft-delete time.</summary>
    public virtual DateTime? DeletedAt
    {
        get;

        set;


    }

    /// <summary>Creator code.</summary>
    public virtual string? CreatedBy
    {
        get;

        set;


    }

    /// <summary>Last updater code.</summary>
    public virtual string? UpdatedBy
    {
        get;

        set;


    }

    /// <summary>Dynamic extensions; never stored in the base table/collection.</summary>
    [Ignore]
    public virtual IDictionary<string,ExtensionValue> Extended
    {
        get;

        set;


    }
    = new Dictionary<string,ExtensionValue>(StringComparer.OrdinalIgnoreCase);


}
