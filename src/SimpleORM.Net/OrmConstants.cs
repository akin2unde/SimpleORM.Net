using System.Linq.Expressions;

using System.Security.Cryptography;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>Operational constants.</summary>
public static class OrmConstants
{

    /// <summary>Default batch.</summary>
    public const int DefaultBatchSize=100;

    /// <summary>Hard max batch.</summary>
    public const int MaximumPhysicalBatchSize=500;

    /// <summary>Minimum code suffix.</summary>
    public const int MinimumCodeLength=4;

}
