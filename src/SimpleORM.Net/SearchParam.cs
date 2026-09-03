namespace SimpleORM.Net.Query;

/// <summary>Provider-neutral selection/search request; pagination is method-level.</summary>
public sealed class SearchParam
{

    /// <summary>Generic text.</summary>
    public string? Search
    {
        get;

        set;

    }

    /// <summary>Optional generic-search field restriction.</summary>
    public List<string> SearchFields
    {
        get;
        set;
    }
    = new List<string>();

    /// <summary>Filters.</summary>
    public List<SearchFilter> Filters
    {
        get;
        set;
    }
    = new List<SearchFilter>();

    /// <summary>Joins.</summary>
    public List<SearchJoin> Joins
    {
        get;
        set;
    }
    = new List<SearchJoin>();

    /// <summary>Main-model projected fields; empty means all persisted.</summary>
    public List<string> Fields
    {
        get;
        set;
    }
    = new List<string>();

    /// <summary>Ordering.</summary>
    public List<SearchOrder> OrderBy
    {
        get;
        set;
    }
    = new List<SearchOrder>();

    /// <summary>User filter combination.</summary>
    public SearchCondition Condition
    {
        get;

        set;

    }
    = SearchCondition.And;

    /// <summary>Include soft-deleted rows.</summary>
    public bool IncludeDeleted
    {
        get;

        set;

    }

    /// <summary>Copies the request.</summary>
    public SearchParam Clone()
    {
        var x = new SearchParam
        {
            Search = Search,
            Condition = Condition,
            IncludeDeleted = IncludeDeleted
        }
        ;

        foreach (var v in SearchFields) x.SearchFields.Add(v);

        foreach (var v in Fields) x.Fields.Add(v);

        foreach (var f in Filters) x.Filters.Add(new SearchFilter
        {
            Field = f.Field,
            Operator = f.Operator,
            Value = f.Value
        }
        );

        foreach (var o in OrderBy) x.OrderBy.Add(new SearchOrder
        {
            Field = o.Field,
            Descending = o.Descending
        }
        );

        foreach (var j in Joins)
        {
            var n = new SearchJoin
            {
                Model = j.Model,
                LocalField = j.LocalField,
                ForeignField = j.ForeignField,
                Type = j.Type,
                Alias = j.Alias
            }
            ;

            foreach (var f in j.Fields) n.Fields.Add(f);

            x.Joins.Add(n);

        }
        return x;

    }

}
