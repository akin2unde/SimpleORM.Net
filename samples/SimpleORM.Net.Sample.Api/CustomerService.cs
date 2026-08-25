using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;
using SimpleORM.Net.Services;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Application service demonstrating the repository-style SimpleORM.Net API.
/// </summary>
/// <remarks>
/// The service intentionally contains examples for the major <see cref="IDataRepository"/>
/// operations so consumers can see how to keep controllers thin while centralizing
/// application data-access behavior in a service layer.
/// </remarks>
public sealed class CustomerService : ICustomerService
{
    private readonly IDataRepository _repository;
    private readonly IDBTransactionManager _transactionManager;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerService"/> class.
    /// </summary>
    /// <param name="repository">The generic SimpleORM.Net data repository.</param>
    /// <param name="transactionManager">The transaction manager used for multi-model operations.</param>
    /// <param name="options">The configured SimpleORM.Net options.</param>
    public CustomerService(
        IDataRepository repository,
        IDBTransactionManager transactionManager,
        SimpleOrmOptions options)
    {
        _repository = repository;
        _transactionManager = transactionManager;
        _options = options;
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> Select(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
    {
        return _repository.Select<Customer>(
            search,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> SelectActive(
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
    {
        return _repository.Select<Customer>(
            customer => customer.Status == CustomerStatus.Active,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <inheritdoc />
    public Task<Customer?> SelectSingle(
        SearchParam search,
        CancellationToken cancellationToken = default)
    {
        return _repository.SelectSingle<Customer>(
            search,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer?> SelectSingleByEmail(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _repository.SelectSingle<Customer>(
            customer => customer.Email == email,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer?> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByCode<Customer>(
            code,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<Customer>> Search(
        string text,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
    {
        return _repository.Search<Customer>(
            text,
            null,
            skip,
            limit,
            cancellationToken,
            batch);
    }

    /// <inheritdoc />
    public Task<long> Count(
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
    {
        return _repository.Count<Customer>(
            search,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> CountActive(
        CancellationToken cancellationToken = default)
    {
        return _repository.Count<Customer>(
            customer => customer.Status == CustomerStatus.Active,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Customer> Save(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        return _repository.Save(
            customer,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Customer>> SaveBatch(
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default,
        int? batch = null)
    {
        return _repository.Save(
            customers,
            cancellationToken,
            batch);
    }

    /// <inheritdoc />
    public string GenerateDebugQuery(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100)
    {
        return _repository.GenerateDebugQuery<Customer>(
            search,
            skip,
            limit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Customer>> SaveCustomersAndInventory(
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Inventory> inventories,
        CancellationToken cancellationToken = default)
    {
        return _transactionManager.Execute(
            async () =>
            {
                var savedCustomers = await _repository.Save(
                    customers,
                    cancellationToken);

                if (inventories.Count > 0)
                {
                    await _repository.Save(
                        inventories,
                        cancellationToken);
                }

                return savedCustomers;
            },
            cancellationToken);
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

        definition.DataState = string.IsNullOrWhiteSpace(definition.Code)
            ? DataState.New
            : DataState.Changed;

        return _repository.Save(
            definition,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DBExtensionDefinition?> PublishExtensionDefinition(
        string definitionCode,
        CancellationToken cancellationToken = default)
    {
        var definition = await _repository.GetByCode<DBExtensionDefinition>(
            definitionCode,
            cancellationToken);

        if (definition is null)
        {
            return null;
        }

        definition.ModelName = nameof(Customer);
        definition.Published = true;
        definition.DataState = DataState.Changed;

        return await _repository.Save(
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

        return _repository.Select<DBExtensionDefinition>(
            search,
            skip: 0,
            limit: 0,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Customer?> SetExtensionValue(
        string customerCode,
        string extensionCode,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByCode<Customer>(
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

        return await _repository.Save(
            customer,
            cancellationToken);
    }
}
