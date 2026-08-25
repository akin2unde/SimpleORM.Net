using SimpleORM.Net.Attributes;

namespace SimpleORM.Net.Models;

/// <summary>Paged or fetch-all result.</summary>
public sealed class PagedResult<T>
{

    /// <summary>Returned data.</summary>
    public IReadOnlyList<T> Data
    {
        get;

        init;

    }
    = Array.Empty<T>();

    /// <summary>Total matching records before skip/limit.</summary>
    public long TotalRecords
    {
        get;

        init;

    }

    /// <summary>Records skipped.</summary>
    public int Skipped
    {
        get;

        init;

    }

    /// <summary>Logical limit; zero means all remaining.</summary>
    public int Limit
    {
        get;

        init;

    }

}
