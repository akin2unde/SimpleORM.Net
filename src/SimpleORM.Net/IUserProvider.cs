using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Current user resolver.</summary>
public interface IUserProvider
{

    /// <summary>Current user.</summary>
    string? GetUserCode();

}
