namespace SimpleORM.Net.Configuration;

/// <summary>Migration startup failure behavior.</summary>
public enum MigrationFailureMode
{

    /// <summary>Stop startup.</summary>
    StopApplication,
    /// <summary>Log and continue.</summary>
    LogAndContinue
}
