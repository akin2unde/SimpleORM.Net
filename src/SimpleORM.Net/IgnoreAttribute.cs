using SimpleORM.Net.Configuration;


namespace SimpleORM.Net.Attributes;


/// <summary>Excludes a property from persistence.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreAttribute:Attribute
{
}
