using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// Provides extension methods and utility operations for MongoDB filters,
/// </summary>
public static class ExtensionUtil
{

    /// <summary>
    /// Renders a MongoDB filter definition as a JSON query string.
    /// </summary>
    /// <typeparam name="T">The MongoDB document type.</typeparam>
    /// <param name="filterDefinition">The filter definition to render.</param>
    /// <returns>The rendered BSON filter as JSON.</returns>
    public static string QueryToString<T>(this FilterDefinition<T> filterDefinition)
    {
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<T>();

        // Render the filter into BsonDocument
        var bsonDocument = filterDefinition.Render(new RenderArgs<T>(documentSerializer, serializerRegistry));

        // Convert to a raw query string
        return bsonDocument.ToJson();
    }

}