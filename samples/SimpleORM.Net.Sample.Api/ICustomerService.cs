using SimpleORM.Net.Models;
using SimpleORM.Net.Query;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Sample.Api;

/// <summary>
/// Application service used by the sample controller.
/// It demonstrates every public method on <c>IDataRepository</c>
/// together with extension definition, publishing and extension-value usage.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Demonstrates IDataRepository.Select(SearchParam,...).
    /// </summary>
    Task<PagedResult<Customer>> Select(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null);

    /// <summary>
    /// Demonstrates IDataRepository.SelectDynamic(SearchParam,...), returning
    /// only the fields requested in SearchParam.Fields and joined projections.
    /// </summary>
    Task<PagedResult<dynamic>> SelectDynamic(
        SearchParam search,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null);

    /// <summary>
    /// Demonstrates IDataRepository.Select(expression,...).
    /// </summary>
    Task<PagedResult<Customer>> SelectActive(
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null);

    /// <summary>
    /// Demonstrates IDataRepository.SelectSingle(SearchParam,...).
    /// </summary>
    Task<Customer?> SelectSingle(
        SearchParam search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.SelectSingle(expression,...).
    /// </summary>
    Task<Customer?> SelectSingleByEmail(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.GetByCode(...).
    /// </summary>
    Task<Customer?> GetByCode(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.Search(...).
    /// </summary>
    Task<PagedResult<Customer>> Search(
        string text,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null);

    /// <summary>
    /// Demonstrates IDataRepository.Count(SearchParam,...).
    /// </summary>
    Task<long> Count(
        SearchParam? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.Count(expression,...).
    /// </summary>
    Task<long> CountActive(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.Save(single).
    /// </summary>
    Task<Customer> Save(
        Customer customer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demonstrates IDataRepository.Save(batch).
    /// </summary>
    Task<IReadOnlyList<Customer>> SaveBatch(
        IReadOnlyList<Customer> customers,
        CancellationToken cancellationToken = default,
        int? batch = null);

    /// <summary>
    /// Demonstrates IDataRepository.GenerateDebugQuery(...).
    /// </summary>
    string GenerateDebugQuery(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100);

    /// <summary>
    /// Creates or updates a Customer extension definition.
    /// When RequirePublish is false, saving publishes automatically.
    /// </summary>
    Task<DBExtensionDefinition> SaveExtensionDefinition(
        DBExtensionDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly publishes an extension definition when RequirePublish is enabled.
    /// </summary>
    Task<DBExtensionDefinition?> PublishExtensionDefinition(
        string definitionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists extension definitions available for Customer.
    /// </summary>
    Task<PagedResult<DBExtensionDefinition>> GetExtensionDefinitions(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an extension value on a Customer and persists it with the Customer save.
    /// </summary>
    Task<Customer?> SetExtensionValue(
        string customerCode,
        string extensionCode,
        object? value,
        CancellationToken cancellationToken = default);
    /// <summary>Demonstrates multiple repository saves in one transaction.</summary>
    Task<IReadOnlyList<Customer>> SaveCustomersAndInventory(
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Inventory> inventories,
        CancellationToken cancellationToken = default);

}
