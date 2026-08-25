namespace SimpleORM.Net.Query;

/// <summary>Join request.</summary>
public sealed class SearchJoin
{

    /// <summary>Joined model type.</summary>
    public Type Model
    {
        get;

        set;

    }
    =typeof(object);

    /// <summary>Local property.</summary>
    public string LocalField
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Foreign property.</summary>
    public string ForeignField
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Join mode.</summary>
    public JoinType Type
    {
        get;

        set;

    }
    =JoinType.Inner;

    /// <summary>Alias.</summary>
    public string? Alias
    {
        get;

        set;

    }

    /// <summary>Projected joined fields.</summary>
    public IList<string> Fields
    {
        get;

    }
    =new List<string>();

}
