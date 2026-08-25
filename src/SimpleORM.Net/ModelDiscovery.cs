using System.Collections.Concurrent;

using System.Reflection;

using SimpleORM.Net.Attributes;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Metadata;

/// <summary>DBModel discovery helper.</summary>
public static class ModelDiscovery
{

    /// <summary>Finds concrete DBModels.</summary>
    public static IReadOnlyList<Type> Discover(IEnumerable<Assembly> assemblies)=>assemblies.Distinct().SelectMany(a=>
    {
        try
        {
            return a.GetTypes();

        }
        catch(ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x=>x is not null).Cast<Type>().ToArray();

        }
    }
    ).Where(t=>t.IsClass&&!t.IsAbstract&&typeof(DBModel).IsAssignableFrom(t)).Distinct().ToArray();

}
