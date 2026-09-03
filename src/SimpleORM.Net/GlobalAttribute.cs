namespace SimpleORM.Net.Attributes;

/// <summary>
/// Marks a model as shared across all tenants. Global models are not tenant scoped
/// even when multi-tenancy is enabled for the application.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    Inherited = true,
    AllowMultiple = false)]
public sealed class GlobalAttribute : Attribute
{
}
