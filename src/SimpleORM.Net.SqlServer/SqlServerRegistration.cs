using System.Collections;

using System.Dynamic;

using System.Globalization;

using System.Reflection;

using Microsoft.Data.SqlClient;

using Microsoft.Extensions.DependencyInjection;

using SimpleORM.Net.Abstractions;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

namespace SimpleORM.Net.SqlServer;

/// <summary>SQL Server provider registration.</summary>
public static class SqlServerRegistration
{

    /// <summary>Registers provider services.</summary>
    public static IServiceCollection AddSimpleOrmSqlServer(this IServiceCollection s)
    {
        s.AddScoped<SqlServerProvider>();

        s.AddScoped<IDatabaseProvider>(x=>x.GetRequiredService<SqlServerProvider>());

        s.AddScoped<IDBQuery>(x=>x.GetRequiredService<SqlServerProvider>());

        s.AddScoped<IDBSchemaSynchronizer,SqlServerSchemaSynchronizer>();

        return s;

    }

}
