using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Batch resolver.</summary>
public static class BatchResolver
{

    /// <summary>Method override, then configured, then 100; capped at 500.</summary>
    public static int Resolve(int? requested,int configured)
    {
        var x=requested??configured;

        if(x<=0)x=100;

        return Math.Min(x,500);

    }

}
