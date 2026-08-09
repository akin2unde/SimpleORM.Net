using System.Collections.Concurrent;


using System.Reflection;


using SimpleORM.Net.Attributes;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Models;


namespace SimpleORM.Net.Metadata;


/// <summary>Metadata cache contract.</summary>
public interface IDBMetadataProvider
{

    /// <summary>Gets metadata.</summary>
    DBModelMetadata GetMetadata<T>() where T:DBModel;


    /// <summary>Gets runtime metadata.</summary>
    DBModelMetadata GetMetadata(Type type);


    /// <summary>Registers metadata.</summary>
    void RegisterModel(Type type);


    /// <summary>All registered metadata.</summary>
    IReadOnlyCollection<DBModelMetadata> GetRegisteredModels();


}
