using SimpleORM.Net.Configuration;


namespace SimpleORM.Net.Attributes;


/// <summary>Excludes a string property from generic text search.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotSearchableAttribute:Attribute
{
}
