using SimpleORM.Net.Configuration;


namespace SimpleORM.Net.Attributes;


/// <summary>Enables dynamic extensions.</summary>
[AttributeUsage(AttributeTargets.Class,Inherited=true)]
public sealed class ExtendableAttribute:Attribute
{
}
