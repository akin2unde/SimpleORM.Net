namespace SimpleORM.Net.Configuration;


/// <summary>SQL migration configuration.</summary>
public sealed class MigrationOptions
{

    /// <summary>Allow destructive SQL changes.</summary>
    public bool AllowDestructiveChanges
    {
        get;

        set;

    }

    /// <summary>Failure behavior.</summary>
    public MigrationFailureMode FailureMode
    {
        get;

        set;

    }
    =MigrationFailureMode.StopApplication;


}
