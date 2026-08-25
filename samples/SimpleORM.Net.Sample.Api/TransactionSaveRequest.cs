namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Request used by the sample transaction endpoint to save customers and
/// inventory records in one logical database transaction.
/// </summary>
public sealed class TransactionSaveRequest
{
    /// <summary>
    /// Gets or sets the customers to save.
    /// </summary>
    public IReadOnlyList<Customer> Customers { get; set; } = [];

    /// <summary>
    /// Gets or sets the inventory records to save in the same transaction.
    /// </summary>
    public IReadOnlyList<Inventory> Inventories { get; set; } = [];
}
