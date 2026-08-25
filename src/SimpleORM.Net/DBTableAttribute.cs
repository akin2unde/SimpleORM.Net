using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.Attributes;

/// <summary>Overrides table/collection name; DBModel inheritance alone makes a model persistent.</summary>
[AttributeUsage(AttributeTargets.Class,Inherited=true)]
public sealed class DBTableAttribute(string name):Attribute
{

    /// <summary>Physical name.</summary>
    public string Name
    {
        get;

    }
    =name;

}
