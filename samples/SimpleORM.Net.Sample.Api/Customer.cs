using SimpleORM.Net.Attributes;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Sample customer model used to demonstrate SimpleORM.Net end to end.
/// </summary>
[Extendable]
public sealed class Customer : DBModel
{
    /// <summary>
    /// Gets or sets the customer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer email address.
    /// </summary>
    [Unique]
    [DBColumn(Size = 150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer phone number.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value that should never be returned by normal ORM reads.
    /// </summary>
    [DefaultOnReturn]
    [NotSearchable]
    [DBColumn(Size = 250)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer status.
    /// </summary>
    public CustomerStatus Status { get; set; }
}
