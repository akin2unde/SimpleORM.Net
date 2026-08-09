using SimpleORM.Net.Configuration;


namespace SimpleORM.Net.Attributes;


/// <summary>Resets a property to default before read results are returned.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultOnReturnAttribute:Attribute
{
}
