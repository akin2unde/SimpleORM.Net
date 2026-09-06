using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// Converts a model into the BSON document that SimpleORM is allowed to persist.
/// </summary>
internal static class MongoDocumentMapper
{
    /// <summary>
    /// Serializes only persisted metadata columns. Ignored properties are never
    /// read or passed to the MongoDB serializer.
    /// </summary>
    public static BsonDocument ToPersistedDocument(
        DBModel model,
        DBModelMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(metadata);

        var document = new BsonDocument();

        foreach (var column in metadata.PersistedColumns)
        {
            var value = column.Property.GetValue(model);
            document[column.ColumnName] = SerializeValue(column, value);
        }

        return document;
    }

    private static BsonValue SerializeValue(
        DBColumnMetadata column,
        object? value)
    {
        if (value is null)
        {
            return BsonNull.Value;
        }

        if (column.IsEnum && value is Enum enumValue)
        {
            return column.EnumStorage == EnumStorage.String
                ? new BsonString(enumValue.ToString())
                : new BsonInt32(Convert.ToInt32(
                    enumValue,
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        var wrapper = new BsonDocument();

        using (var writer = new BsonDocumentWriter(wrapper))
        {
            writer.WriteStartDocument();
            writer.WriteName("value");
            var serializer = BsonSerializer.LookupSerializer(
                column.PropertyType);
            var context = BsonSerializationContext.CreateRoot(writer);
            var arguments = new BsonSerializationArgs
            {
                NominalType = column.PropertyType
            };

            serializer.Serialize(context, arguments, value);
            writer.WriteEndDocument();
        }

        return wrapper["value"];
    }
}
