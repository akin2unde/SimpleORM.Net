using SimpleORM.Net.Configuration;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;
using SimpleORM.Net.Services;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Sample application service showing how a real application can wrap
/// <see cref="IDataService{Customer}"/> instead of injecting the ORM directly into controllers.
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private readonly IDataService<Customer> _customers;
    private readonly IDataService<DBExtensionDefinition> _extensionDefinitions;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerService"/> class.
    /// </summary>
    public CustomerService(
        IDataService<Customer> customers,
        IDataService<DBExtensionDefinition> extensionDefinitions,
        SimpleOrmOptions options)
    {
        _customers = customers;
        _extensionDefinitions = extensionDefinitions;
        _options = options;
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> Select(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100,
        int? batch = null,
        CancellationToken cancellationToken = default)
    {
        return _customers.Select(
            search,
            skip,
            limit,
            batch,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> SelectActive(
        int skip = 0,
        int limit = 100,
        int? batch = null,
        CancellationToken cancellationToken = default)
    {
        return _customers.Select(
            customer => customer.Status == CustomerStatus.Active,
            search: null,
            skip: skip,
            limit: limit,
            batch: batch,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer?> SelectSingle(
        SearchParam search,
        CancellationToken cancellationToken = default)
    {
        return _customers.SelectSingle(
            search,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer?> SelectSingleByEmail(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _customers.SelectSingle(
            customer => customer.Email == email,
            search: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer?> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        return _customers.GetByCode(
            code,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> Search(
        string text,
        int skip = 0,
        int limit = 100,
        int? batch = null,
        CancellationToken cancellationToken = default)
    {
        return _customers.Search(
            text,
            searchParam: null,
            skip: skip,
            limit: limit,
            batch: batch,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> Count(
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
    {
        return _customers.Count(
            search,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> CountActive(
        CancellationToken cancellationToken = default)
    {
        return _customers.Count(
            customer => customer.Status == CustomerStatus.Active,
            search: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer> Save(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        return _customers.Save(
            customer,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Customer>> SaveBatch(
        IReadOnlyList<Customer> customers,
        int? batch = null,
        CancellationToken cancellationToken = default)
    {
        return _customers.Save(
            customers,
            batch,
            cancellationToken);
    }

    /// <inheritdoc />
    public string GenerateDebugQuery(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100)
    {
        return _customers.GenerateDebugQuery(
            search,
            skip,
            limit);
    }

    /// <inheritdoc />
    public Task<DBExtensionDefinition> SaveExtensionDefinition(
        DBExtensionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        definition.ModelName = nameof(Customer);

        if (!_options.Extensions.RequirePublish)
        {
            definition.Published = true;
        }

        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            definition.DataState = DataState.New;
        }
        else if (definition.DataState == DataState.Unchanged)
        {
            definition.DataState = DataState.Changed;
        }

        return _extensionDefinitions.Save(
            definition,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DBExtensionDefinition?> PublishExtensionDefinition(
        string definitionCode,
        CancellationToken cancellationToken = default)
    {
        var definition = await _extensionDefinitions.GetByCode(
            definitionCode,
            cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.ModelName = nameof(Customer);
        definition.Published = true;
        definition.DataState = DataState.Changed;

        return await _extensionDefinitions.Save(
            definition,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<DBExtensionDefinition>> GetExtensionDefinitions(
        CancellationToken cancellationToken = default)
    {
        var search = new SearchParam();

        search.Filters.Add(
            new SearchFilter
            {
                Field = nameof(DBExtensionDefinition.ModelName),
                Operator = SearchOperator.Equal,
                Value = nameof(Customer)
            });

        if (_options.Extensions.RequirePublish)
        {
            search.Filters.Add(
                new SearchFilter
                {
                    Field = nameof(DBExtensionDefinition.Published),
                    Operator = SearchOperator.Equal,
                    Value = true
                });
        }

        return _extensionDefinitions.Select(
            search,
            skip: 0,
            limit: 0,
            batch: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Customer?> SetExtensionValue(
        string customerCode,
        string extensionCode,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByCode(
            customerCode,
            cancellationToken);

        if (customer is null)
        {
            return null;
        }

        if (!customer.Extended.TryGetValue(
                extensionCode,
                out var extension))
        {
            throw new InvalidOperationException(
                $"Extension '{extensionCode}' is not published for Customer.");
        }

        extension.Data = value;
        customer.DataState = DataState.Changed;

        return await _customers.Save(
            customer,
            cancellationToken);
    }
}
