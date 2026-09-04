using SimpleORM.Net.Attributes;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Tests;

[DisableConcurrencyCheck]
internal sealed class NoConcurrencyModel : DBModel
{
    public string Name { get; set; } = string.Empty;
}
