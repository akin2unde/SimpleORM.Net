using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net;


internal sealed class NullUser : IUserProvider
{

    public string? GetUserCode() => null;


}
