namespace SimpleORM.Net.Configuration;


/// <summary>Root ORM options.</summary>
public sealed class SimpleOrmOptions
{

    /// <summary>Selected provider.</summary>
    public DatabaseType Database
    {
        get;

        set;

    }
    =DatabaseType.SqlServer;


    /// <summary>Connection options.</summary>
    public DatabaseConnectionOptions Connection
    {
        get;

    }
    =new();


    /// <summary>Default string size.</summary>
    public int DefaultStringLength
    {
        get;

        set;

    }
    =50;


    /// <summary>Global enum storage.</summary>
    public EnumStorage EnumStorage
    {
        get;

        set;

    }
    =EnumStorage.String;


    /// <summary>Startup synchronization flag.</summary>
    public bool AutoMigration
    {
        get;

        set;

    }
    =true;


    /// <summary>Code generation.</summary>
    public CodeGenerationOptions CodeGeneration
    {
        get;

    }
    =new();


    /// <summary>Batch settings.</summary>
    public BatchOptions Batch
    {
        get;

    }
    =new();


    /// <summary>Tenant settings.</summary>
    public MultiTenancyOptions MultiTenancy
    {
        get;

    }
    =new();


    /// <summary>Search settings.</summary>
    public SearchOptions Search
    {
        get;

    }
    =new();


    /// <summary>Audit settings.</summary>
    public AuditTrailOptions AuditTrail
    {
        get;

    }
    =new();


    /// <summary>Extension settings.</summary>
    public ExtensionOptions Extensions
    {
        get;

    }
    =new();


    /// <summary>Error-log settings.</summary>
    public ErrorLogOptions ErrorLog
    {
        get;

    }
    =new();


    /// <summary>Migration settings.</summary>
    public MigrationOptions Migrations
    {
        get;

    }
    =new();


}
