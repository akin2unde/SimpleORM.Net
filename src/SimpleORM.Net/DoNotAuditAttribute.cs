using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.Attributes;

/// <summary>Excludes a property from audit old/new JSON.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DoNotAuditAttribute:Attribute
{
}
