using System.Linq.Expressions;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>
/// Provides repository-style CRUD, search, count, and debug-query operations for
/// every model that inherits from <see cref="DBModel"/>.
/// </summary>
/// <remarks>
/// A single <see cref="IDataRepository"/> can be injected and reused for different
/// model types by specifying the model type on each method call.
/// </remarks>
public interface IDataRepository
{
    /// <summary>
    /// Selects records using an optional <see cref="SearchParam"/>.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="search">Optional filters, joins, projections, and ordering.</param>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size. Physical batches are capped by the ORM maximum.</param>
    /// <returns>A paged result containing the selected models and total matching record count.</returns>
    Task<PagedResult<T>> Select<T>(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Selects records using paging parameters without requiring a
    /// <see cref="SearchParam"/> instance.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size. Physical batches are capped by the ORM maximum.</param>
    /// <returns>A paged result containing the selected models and total matching record count.</returns>
    Task<PagedResult<T>> Select<T>(
        int skip,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Selects records using a strongly typed expression.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="expression">Expression used to filter records.</param>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size.</param>
    /// <returns>A paged result containing the selected models and total matching record count.</returns>
    Task<PagedResult<T>> Select<T>(
        Expression<Func<T, bool>> expression,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Selects records using a strongly typed expression together with additional
    /// <see cref="SearchParam"/> query options.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="expression">Expression used to filter records.</param>
    /// <param name="search">Additional filters, joins, projections, or ordering.</param>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size.</param>
    /// <returns>A paged result containing the selected models and total matching record count.</returns>
    Task<PagedResult<T>> Select<T>(
        Expression<Func<T, bool>> expression,
        SearchParam? search,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Selects records as dynamic objects containing only the fields explicitly
    /// requested through <see cref="SearchParam.Fields"/> and joined-field projections.
    /// </summary>
    /// <typeparam name="T">The DBModel type used to build the query.</typeparam>
    /// <param name="search">Filters, joins, selected fields, and ordering.</param>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size.</param>
    /// <returns>A paged result of dynamic objects containing only selected fields.</returns>
    Task<PagedResult<dynamic>> SelectDynamic<T>(
        SearchParam search,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Selects the first record matching an optional <see cref="SearchParam"/>.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="search">Optional filters and query options.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The matching model, or <see langword="null"/> when no record matches.</returns>
    Task<T?> SelectSingle<T>(
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Selects the first record matching a strongly typed expression.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="expression">Expression used to filter records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The matching model, or <see langword="null"/> when no record matches.</returns>
    Task<T?> SelectSingle<T>(
        Expression<Func<T, bool>> expression,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Selects the first record matching an expression and additional
    /// <see cref="SearchParam"/> query options.
    /// </summary>
    /// <typeparam name="T">The DBModel type to select.</typeparam>
    /// <param name="expression">Expression used to filter records.</param>
    /// <param name="search">Additional query options.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The matching model, or <see langword="null"/> when no record matches.</returns>
    Task<T?> SelectSingle<T>(
        Expression<Func<T, bool>> expression,
        SearchParam? search,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Gets a single record by its globally unique ORM code.
    /// </summary>
    /// <typeparam name="T">The DBModel type to retrieve.</typeparam>
    /// <param name="code">The record code.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The matching model, or <see langword="null"/> when the code does not exist.</returns>
    Task<T?> GetByCode<T>(
        string code,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Searches all eligible string properties for the supplied text.
    /// </summary>
    /// <typeparam name="T">The DBModel type to search.</typeparam>
    /// <param name="text">Text to search for.</param>
    /// <param name="search">Optional additional query options.</param>
    /// <param name="skip">The number of matching records to skip.</param>
    /// <param name="limit">The maximum number of records to return. Zero means all matching records.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size.</param>
    /// <returns>A paged result containing matching models.</returns>
    Task<PagedResult<T>> Search<T>(
        string text,
        SearchParam? search = null,
        int skip = 0,
        int limit = 100,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Counts records matching an optional <see cref="SearchParam"/>.
    /// </summary>
    /// <typeparam name="T">The DBModel type to count.</typeparam>
    /// <param name="search">Optional filters.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of matching records.</returns>
    Task<long> Count<T>(
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Counts records matching a strongly typed expression.
    /// </summary>
    /// <typeparam name="T">The DBModel type to count.</typeparam>
    /// <param name="expression">Expression used to filter records.</param>
    /// <param name="search">Optional additional filters or query options.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The number of matching records.</returns>
    Task<long> Count<T>(
        Expression<Func<T, bool>> expression,
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Saves one model according to its <see cref="DBModel.DataState"/>.
    /// </summary>
    /// <typeparam name="T">The DBModel type to save.</typeparam>
    /// <param name="model">The model to save.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The saved model.</returns>
    Task<T> Save<T>(
        T model,
        CancellationToken cancellationToken = default)
        where T : DBModel;

    /// <summary>
    /// Saves multiple models according to their individual <see cref="DBModel.DataState"/> values.
    /// </summary>
    /// <typeparam name="T">The DBModel type to save.</typeparam>
    /// <param name="models">Models to save.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <param name="batch">Optional batch size.</param>
    /// <returns>The saved models.</returns>
    Task<IReadOnlyList<T>> Save<T>(
        IEnumerable<T> models,
        CancellationToken cancellationToken = default,
        int? batch = null)
        where T : DBModel;

    /// <summary>
    /// Generates a provider-specific query with values embedded for debugging purposes.
    /// </summary>
    /// <typeparam name="T">The DBModel type used to build the query.</typeparam>
    /// <param name="search">Optional query options.</param>
    /// <param name="skip">The number of rows to skip.</param>
    /// <param name="limit">The maximum number of rows represented in the query.</param>
    /// <returns>A readable provider-specific query string.</returns>
    /// <remarks>
    /// The returned string is intended for inspection only and should not be used as an execution command.
    /// </remarks>
    string GenerateDebugQuery<T>(
        SearchParam? search = null,
        int skip = 0,
        int limit = 100)
        where T : DBModel;
}
