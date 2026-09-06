using SimpleORM.Net.Models;

namespace SimpleORM.Net.Query;

/// <summary>
/// Converts transport-level search values into the CLR types declared by a model.
/// </summary>
public interface ISearchParamNormalizer
{
    /// <summary>
    /// Clones and normalizes a provider-neutral search request for the specified model.
    /// </summary>
    SearchParam Normalize<T>(SearchParam? search)
        where T : DBModel;
}
