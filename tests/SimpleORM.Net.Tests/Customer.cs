using SimpleORM.Net.Attributes;

using SimpleORM.Net.Models;

namespace SimpleORM.Net.Tests;

internal sealed class Customer : DBModel
{

    public string Name
    {
        get;

        set;

    }
    = string.Empty;

    [NotSearchable]
    public string Secret
    {
        get;

        set;

    }
    = string.Empty;

}
