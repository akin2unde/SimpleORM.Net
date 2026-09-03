namespace SimpleORM.Net.Attributes;

/// <summary>
/// Configures automatic hard deletion of stale records for a model.
/// </summary>
/// <remarks>
/// Records are considered stale when their CreatedAt value is older than the configured
/// retention period. Cron expressions use standard five-field cron syntax and UTC time.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class,
    Inherited = true,
    AllowMultiple = false)]
public sealed class AutoDeleteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new automatic deletion policy.
    /// </summary>
    /// <param name="olderThanDays">Delete records older than this number of days.</param>
    /// <param name="cron">Standard five-field cron expression evaluated in UTC.</param>
    public AutoDeleteAttribute(
        int olderThanDays,
        string cron)
    {
        if (olderThanDays <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(olderThanDays),
                "Retention days must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(cron))
        {
            throw new ArgumentException(
                "Cron expression is required.",
                nameof(cron));
        }

        OlderThanDays = olderThanDays;
        Cron = cron;
    }

    /// <summary>Gets the number of days records are retained.</summary>
    public int OlderThanDays { get; }

    /// <summary>Gets the standard five-field UTC cron expression.</summary>
    public string Cron { get; }
}
