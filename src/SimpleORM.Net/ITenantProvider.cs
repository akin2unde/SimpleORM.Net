using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Current tenant resolver.</summary>
public interface ITenantProvider
{

    /// <summary>Current tenant.</summary>
    string? GetTenant();

}
