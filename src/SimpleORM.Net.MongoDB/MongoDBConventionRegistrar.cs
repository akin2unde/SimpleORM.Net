using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using SimpleORM.Net.Attributes;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Models;
using SimpleORM.Net.MongoDB.Options;

namespace SimpleORM.Net.MongoDB.Configuration;

/// <summary>
/// Registers MongoDB serialization conventions required by SimpleORM.
/// </summary>
internal static class MongoDBConventionRegistrar
{
    private const string IgnoreExtraElementsConventionName =
        "SimpleORM.IgnoreExtraElements";

    private const string PersistenceConventionName =
        "SimpleORM.Persistence";

    private static readonly object RegistrationLock = new();
    private static bool _ignoreExtraElementsRegistered;
    private static bool _persistenceConventionRegistered;

    /// <summary>
    /// Registers conventions configured for the MongoDB provider.
    /// </summary>
    /// <param name="options">The configured MongoDB options.</param>
    public static void Register(MongoDBOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.IgnoreMongoId)
        {
            RegisterIgnoreExtraElementsConvention();
        }
    }

    /// <summary>
    /// Registers persistence conventions that depend on the provider-neutral
    /// SimpleORM configuration.
    /// </summary>
    public static void RegisterPersistence(EnumStorage enumStorage)
    {
        lock (RegistrationLock)
        {
            if (_persistenceConventionRegistered)
            {
                return;
            }

            var conventionPack = new ConventionPack();

            conventionPack.Add(
                new EnumRepresentationConvention(
                    enumStorage == EnumStorage.String
                        ? BsonType.String
                        : BsonType.Int32));

            conventionPack.AddPostProcessingConvention(
                "SimpleOrmIgnoreMembers",
                classMap =>
                {
                    var ignoredProperties = classMap.ClassType
                        .GetProperties(
                            BindingFlags.Public
                            | BindingFlags.Instance
                            | BindingFlags.DeclaredOnly)
                        .Where(property =>
                            property.IsDefined(
                                typeof(IgnoreAttribute),
                                inherit: true));

                    foreach (var property in ignoredProperties)
                    {
                        classMap.UnmapMember(property);
                    }
                });

            ConventionRegistry.Register(
                PersistenceConventionName,
                conventionPack,
                UsesSimpleOrmPersistenceAttributes);

            _persistenceConventionRegistered = true;
        }
    }

    private static bool UsesSimpleOrmPersistenceAttributes(Type type)
    {
        if (typeof(DBModel).IsAssignableFrom(type))
        {
            return true;
        }

        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.IsDefined(
                typeof(IgnoreAttribute),
                inherit: true));
    }

    private static void RegisterIgnoreExtraElementsConvention()
    {
        lock (RegistrationLock)
        {
            if (_ignoreExtraElementsRegistered)
            {
                return;
            }

            var conventionPack = new ConventionPack
            {
                new IgnoreExtraElementsConvention(true)
            };

            ConventionRegistry.Register(
                IgnoreExtraElementsConventionName,
                conventionPack,
                type => typeof(DBModel).IsAssignableFrom(type));

            _ignoreExtraElementsRegistered = true;
        }
    }
}
