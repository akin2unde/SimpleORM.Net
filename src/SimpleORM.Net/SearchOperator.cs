namespace SimpleORM.Net.Query;

/// <summary>Provider-neutral filter operators.</summary>
public enum SearchOperator
{

    /// <summary>Equal.</summary>
    Equal,
    /// <summary>Not equal.</summary>
    NotEqual,
    /// <summary>Greater than.</summary>
    GreaterThan,
    /// <summary>Greater/equal.</summary>
    GreaterThanOrEqual,
    /// <summary>Less than.</summary>
    LessThan,
    /// <summary>Less/equal.</summary>
    LessThanOrEqual,
    /// <summary>Contains.</summary>
    Contains,
    /// <summary>Starts with.</summary>
    StartsWith,
    /// <summary>Ends with.</summary>
    EndsWith,
    /// <summary>In list.</summary>
    In,
    /// <summary>Not in list.</summary>
    NotIn,
    /// <summary>Is null.</summary>
    IsNull,
    /// <summary>Is not null.</summary>
    IsNotNull,
    /// <summary>Inclusive range.</summary>
    Between,
    /// <summary>Outside range.</summary>
    NotBetween
}
