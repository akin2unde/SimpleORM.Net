using SimpleORM.Net.Attributes;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Tests;

[AutoDelete(60, "0 0 1 * *")]
internal sealed class AutoDeleteModel : DBModel
{
}
