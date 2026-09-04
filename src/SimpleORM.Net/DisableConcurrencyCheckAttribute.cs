namespace SimpleORM.Net.Attributes;

/// <summary>
/// Disables optimistic concurrency checks for the decorated model.
/// Use only when last-write-wins behavior is intentionally acceptable.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DisableConcurrencyCheckAttribute : Attribute
{
}
