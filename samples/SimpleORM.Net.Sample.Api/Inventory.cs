using SimpleORM.Net.Models;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Sample inventory model used by the transaction example.
/// </summary>
public sealed class Inventory : DBModel
{
    /// <summary>
    /// Gets or sets the related product code.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the currently available quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Returns the code prefix used when generating inventory codes.
    /// </summary>
    /// <returns>The <c>INV</c> prefix.</returns>
    public override string GetPrefix()
    {
        return "INV";
    }
}
