using SimpleORM.Net.Attributes;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Tests;

[Global]
internal sealed class GlobalModel : DBModel
{
    public string Name { get; set; } = string.Empty;
}
