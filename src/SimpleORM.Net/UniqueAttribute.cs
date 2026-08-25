using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.Attributes;

/// <summary>Marks a property unique; repeated group name defines a composite unique group.</summary>
[AttributeUsage(AttributeTargets.Property,AllowMultiple=true)]
public sealed class UniqueAttribute:Attribute
{

    /// <summary>Single-column unique.</summary>
    public UniqueAttribute()
    {
    }

    /// <summary>Composite group.</summary>
    public UniqueAttribute(string group)=>Group=group;

    /// <summary>Group name.</summary>
    public string? Group
    {
        get;

    }

}
