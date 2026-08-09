using SimpleORM.Net.Abstractions;

namespace SimpleORM.Net;


internal sealed class NullTenant : ITenantProvider
{

    public string? GetTenantCode() => null;


}
