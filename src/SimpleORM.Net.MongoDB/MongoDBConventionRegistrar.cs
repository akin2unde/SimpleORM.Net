using MongoDB.Bson.Serialization.Conventions;
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

    private static readonly object RegistrationLock = new();

    private static bool _ignoreExtraElementsRegistered;

    /// <summary>
    /// Registers conventions configured for the MongoDB provider.
    /// </summary>
    /// <param name="options">
    /// The configured MongoDB options.
    /// </param>
    public static void Register(MongoDBOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.IgnoreMongoId)
        {
            RegisterIgnoreExtraElementsConvention();
        }
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