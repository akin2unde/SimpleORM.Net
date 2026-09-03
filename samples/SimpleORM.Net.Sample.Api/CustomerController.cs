using Microsoft.AspNetCore.Mvc;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Thin HTTP controller that delegates application behavior to <see cref="ICustomerService"/>.
/// </summary>
[ApiController]
[Route("customer")]
public sealed class CustomerController : ControllerBase
{
    private readonly ICustomerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerController"/> class.
    /// </summary>
    /// <param name="service">The customer application service.</param>
    public CustomerController(ICustomerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns customers using a SearchParam request.
    /// </summary>
    [HttpPost("Select")]
    public Task<PagedResult<Customer>> Select(
        [FromBody] SearchParam? search,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default,
        [FromQuery] int? batch = null)
    {
        return _service.Select(
            search,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <summary>Returns only the customer fields selected in the request.</summary>
    [HttpPost("SelectDynamic")]
    public Task<PagedResult<dynamic>> SelectDynamic(
        [FromBody] SearchParam search,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default,
        [FromQuery] int? batch = null)
    {
        return _service.SelectDynamic(
            search,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <summary>
    /// Demonstrates the expression-based Select overload by returning active customers.
    /// </summary>
    [HttpGet("SelectActive")]
    public Task<PagedResult<Customer>> SelectActive(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default,
        [FromQuery] int? batch = null)
    {
        return _service.SelectActive(
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <summary>
    /// Returns the first customer matching a SearchParam request.
    /// </summary>
    [HttpPost("SelectSingle")]
    public Task<Customer?> SelectSingle(
        [FromBody] SearchParam search,
        CancellationToken cancellationToken = default)
    {
        return _service.SelectSingle(
            search,
            cancellationToken);
    }

    /// <summary>
    /// Demonstrates the expression-based SelectSingle overload.
    /// </summary>
    [HttpGet("SelectSingleByEmail")]
    public Task<Customer?> SelectSingleByEmail(
        [FromQuery] string email,
        CancellationToken cancellationToken = default)
    {
        return _service.SelectSingleByEmail(
            email,
            cancellationToken);
    }

    /// <summary>
    /// Gets a customer by globally unique Code.
    /// </summary>
    [HttpGet("GetByCode/{code}")]
    public Task<Customer?> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        return _service.GetByCode(
            code,
            cancellationToken);
    }

    /// <summary>
    /// Searches all eligible string properties.
    /// </summary>
    [HttpGet("Search")]
    public Task<PagedResult<Customer>> Search(
        [FromQuery] string text,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default,
        [FromQuery] int? batch = null)
    {
        return _service.Search(
            text,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <summary>
    /// Counts customers matching an optional SearchParam request.
    /// </summary>
    [HttpPost("Count")]
    public Task<long> Count(
        [FromBody] SearchParam? search,
        CancellationToken cancellationToken = default)
    {
        return _service.Count(
            search,
            cancellationToken);
    }

    /// <summary>
    /// Demonstrates the expression-based Count overload.
    /// </summary>
    [HttpGet("CountActive")]
    public Task<long> CountActive(
        CancellationToken cancellationToken = default)
    {
        return _service.CountActive(
            cancellationToken);
    }

    /// <summary>
    /// Saves one customer. DataState decides insert, update or delete.
    /// </summary>
    [HttpPost("Save")]
    public Task<Customer> Save(
        [FromBody] Customer customer,
        CancellationToken cancellationToken = default)
    {
        return _service.Save(
            customer,
            cancellationToken);
    }

    /// <summary>
    /// Saves a batch of customers in the ORM transaction pipeline.
    /// </summary>
    [HttpPost("SaveBatch")]
    public Task<IReadOnlyList<Customer>> SaveBatch(
        [FromBody] IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default,
        [FromQuery] int? batch = null)
    {
        return _service.SaveBatch(
            customers,
            cancellationToken,
            batch);
    }

    /// <summary>
    /// Produces a provider-specific debug query with values embedded for inspection.
    /// </summary>
    [HttpPost("GenerateDebugQuery")]
    public ActionResult<string> GenerateDebugQuery(
        [FromBody] SearchParam? search,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100)
    {
        return Ok(
            _service.GenerateDebugQuery(
                search,
                skip,
                limit));
    }

    /// <summary>
    /// Creates or updates a Customer extension definition.
    /// </summary>
    [HttpPost("Extension/Definition")]
    public Task<DBExtensionDefinition> SaveExtensionDefinition(
        [FromBody] DBExtensionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        return _service.SaveExtensionDefinition(
            definition,
            cancellationToken);
    }

    /// <summary>
    /// Explicitly publishes an extension definition when publish workflow is enabled.
    /// </summary>
    [HttpPost("Extension/Publish/{definitionCode}")]
    public Task<DBExtensionDefinition?> PublishExtensionDefinition(
        string definitionCode,
        CancellationToken cancellationToken = default)
    {
        return _service.PublishExtensionDefinition(
            definitionCode,
            cancellationToken);
    }

    /// <summary>
    /// Lists Customer extension definitions.
    /// </summary>
    [HttpGet("Extension/Definitions")]
    public Task<PagedResult<DBExtensionDefinition>> GetExtensionDefinitions(
        CancellationToken cancellationToken = default)
    {
        return _service.GetExtensionDefinitions(
            cancellationToken);
    }

    /// <summary>
    /// Sets one extension value on a Customer and saves the Customer.
    /// </summary>
    [HttpPost("{customerCode}/Extension/{extensionCode}")]
    public Task<Customer?> SetExtensionValue(
        string customerCode,
        string extensionCode,
        [FromBody] object? value,
        CancellationToken cancellationToken = default)
    {
        return _service.SetExtensionValue(
            customerCode,
            extensionCode,
            value,
            cancellationToken);
    }

    /// <summary>Demonstrates saving two model types in one SimpleORM transaction.</summary>
    [HttpPost("SaveTransaction")]
    public Task<IReadOnlyList<Customer>> SaveTransaction(
        [FromBody] TransactionSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        return _service.SaveCustomersAndInventory(
            request.Customers,
            request.Inventories,
            cancellationToken);
    }
}
