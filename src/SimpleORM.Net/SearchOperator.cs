using System.Text.Json.Serialization;

namespace SimpleORM.Net.Query;

/// <summary>Provider-neutral filter operators.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchOperator
{

    /// <summary>Equal.</summary>
    EQ,
    /// <summary>Not equal.</summary>
    NEQ,
    /// <summary>Greater than.</summary>
    GT,
    /// <summary>Greater/equal.</summary>
    GTE,
    /// <summary>Less than.</summary>
    LT,
    /// <summary>Less/equal.</summary>
    LTE,
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
