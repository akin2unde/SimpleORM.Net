using SimpleORM.Net.Attributes;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Tests;

internal sealed class Customer : DBModel
{
    public CustomerStatus Status
    {
        get;

        set;

    }

    public string Name
    {
        get;

        set;

    }
    = string.Empty;

    [NotSearchable]
    public string Secret
    {
        get;

        set;

    }
    = string.Empty;

    [Ignore]
    public string? TemporaryValue { get; set; }

    [Ignore]
    public IgnoredMongoModel IgnoredModel
    {
        get => throw new InvalidOperationException(
            "MongoDB must never read an ignored property.");
        set { }
    }

    [DBColumn(Name = "customer_name")]
    public string RenamedValue { get; set; } = string.Empty;

    public List<ProductImageValue> Images { get; set; } = [];

}

internal sealed class IgnoredMongoModel
{
    public string Value { get; set; } = string.Empty;
}

internal sealed class ProductImageValue
{
    public string Url { get; set; } = string.Empty;

    [Ignore]
    public IgnoredMongoModel ImageFile
    {
        get => throw new InvalidOperationException(
            "MongoDB must never serialize a nested ignored property.");
        set { }
    }

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }
}

internal enum CustomerStatus
{
    Inactive,
    Active
}
