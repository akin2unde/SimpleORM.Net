using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Services;

namespace SimpleORM.Net;

/// <summary>Assemblies scanned for DBModel types.</summary>
public sealed class ModelAssemblyRegistry(IEnumerable<Assembly> assemblies)
{

    /// <summary>Assemblies.</summary>
    public IReadOnlyList<Assembly> Assemblies
    {
        get;

    }
    =assemblies.Distinct().ToArray();

}
