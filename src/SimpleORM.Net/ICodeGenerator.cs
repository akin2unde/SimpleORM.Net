using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Random code generator.</summary>
public interface ICodeGenerator
{

    /// <summary>One code.</summary>
    string Generate<T>() where T:DBModel;


}
