namespace SimpleORM.Net.Query;

/// <summary>Single filter.</summary>
public sealed class SearchFilter
{

    /// <summary>Property.</summary>
    public string Field
    {
        get;

        set;

    }
    =string.Empty;

    /// <summary>Operator.</summary>
    public SearchOperator Operator
    {
        get;

        set;

    }

    /// <summary>Value/list/range.</summary>
    public object? Value
    {
        get;

        set;

    }

}
