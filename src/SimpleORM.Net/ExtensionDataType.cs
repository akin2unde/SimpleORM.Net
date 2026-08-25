using SimpleORM.Net.Attributes;

namespace SimpleORM.Net.Models;

/// <summary>Supported dynamic extension value types.</summary>
public enum ExtensionDataType
{

    /// <summary>String.</summary>
    String,
    /// <summary>Int32.</summary>
    Integer,
    /// <summary>Int64.</summary>
    Long,
    /// <summary>Decimal.</summary>
    Decimal,
    /// <summary>Double.</summary>
    Double,
    /// <summary>Boolean.</summary>
    Boolean,
    /// <summary>Date.</summary>
    Date,
    /// <summary>DateTime.</summary>
    DateTime,
    /// <summary>Guid.</summary>
    Guid,
    /// <summary>JSON.</summary>
    Json
}
