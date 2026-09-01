namespace SimpleORM.Net.MongoDB.Options;

/// <summary>
/// Contains configuration used by the MongoDB provider.
/// </summary>
public sealed class MongoDBOptions
{
    /// <summary>
    /// Gets or sets whether MongoDB fields that do not have matching
    /// model properties should be ignored during deserialization.
    /// </summary>
    /// <remarks>
    /// When false, MongoDB throws a FormatException when a document
    /// contains a field that is not represented by the model.
    ///
    /// When true, unknown fields are ignored.
    /// </remarks>
    public bool IgnoreMongoId { get; set; } = false;
}