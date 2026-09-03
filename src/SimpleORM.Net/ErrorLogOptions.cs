namespace SimpleORM.Net.Configuration;

/// <summary>Error-log configuration.</summary>
public sealed class ErrorLogOptions
{
    /// <summary>Gets or sets whether database error logging is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets whether sanitized JSON request payloads are captured.</summary>
    public bool IncludePayload { get; set; } = true;

    /// <summary>Gets or sets the maximum stored payload length.</summary>
    public int MaxPayloadLength { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets whether stale error logs are automatically hard deleted.
    /// </summary>
    public bool AutoDeleteEnabled { get; set; }

    /// <summary>
    /// Gets or sets how many days error logs are retained before they are stale.
    /// </summary>
    public int RetentionDays { get; set; } = 60;

    /// <summary>
    /// Gets or sets the standard five-field UTC cron expression used to clean stale
    /// error logs. The default runs at midnight on the first day of every month.
    /// </summary>
    public string CleanupCron { get; set; } = "0 0 1 * *";
}
