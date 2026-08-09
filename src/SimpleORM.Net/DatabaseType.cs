namespace SimpleORM.Net.Configuration;


/// <summary>Database provider selection.</summary>
public enum DatabaseType
{

    /// <summary>SQL Server.</summary>
    SqlServer,
    /// <summary>MongoDB.</summary>
    MongoDb,
    /// <summary>Future PostgreSQL.</summary>
    PostgreSql,
    /// <summary>Future MySQL.</summary>
    MySql,
    /// <summary>Future SQLite.</summary>
    Sqlite,
    /// <summary>Future Cassandra.</summary>
    Cassandra
}
