using SimpleORM.Net.Configuration;


namespace SimpleORM.Net.Attributes;


/// <summary>Uses physical delete when DataState is Removed.</summary>
[AttributeUsage(AttributeTargets.Class,Inherited=true)]
public sealed class HardDeleteAttribute:Attribute
{
}
