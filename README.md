# SimpleORM.Net

SimpleORM.Net is a lightweight, convention-first ORM toolkit for **.NET 10**, authored by **Akintunde Morakinyo** for the repository `https://github.com/akin2unde/SimpleORM.Net`.

The MVP supports **SQL Server** and **MongoDB** while preserving a provider-neutral core for future PostgreSQL, MySQL, SQLite, Cassandra, and cross-database data movement.

## Packages

- `SimpleORM.Net` – core models, metadata cache, DataState CRUD orchestration, batching, transactions, query contracts.
- `SimpleORM.Net.SqlServer` – SQL Server provider and automatic relational schema synchronization.
- `SimpleORM.Net.MongoDB` – MongoDB provider and SimpleORM-owned index synchronization only.
- `SimpleORM.Net.AspNetCore` – JWT tenant/user resolution, optional error middleware, optional hybrid RSA/AES request/response encryption.
- `SimpleORM.Net.Http` – typed outbound HTTP wrapper.

## Core model

```csharp
[Extendable]
public sealed class Customer : DBModel
{
    public string Name { get; set; } = string.Empty;

    [Unique]
    [DBColumn(Size = 100)]
    public string Email { get; set; } = string.Empty;

    [DefaultOnReturn]
    public string PasswordHash { get; set; } = string.Empty;
}
```

Any concrete type inheriting `DBModel` is automatically a database model. Use `[DBTable("...")]` only to override its table/collection name.

`Code` is unique automatically. Default generated form is `PREFIX-XXXXXXXXXX`, e.g. `CUS-A7K2M9Q4XZ`. The suffix defaults to 10 characters, configurable globally or with `[DBCode]` per model.

## One Save API

```csharp
customer.DataState = DataState.New;      // insert
customer.DataState = DataState.Changed;  // update
customer.DataState = DataState.Removed;  // soft delete unless [HardDelete]

await service.Save(customer);
```

Batch Save uses a transaction, reuses an existing transaction when present, and physically splits work into batches. Default Save/Select batch size is 100. No physical batch exceeds 500 even when a larger value is requested.

## Reads

```csharp
var page = await service.Select(skip: 0, limit: 100);
var all = await service.Select(limit: 0); // limit 0 = all remaining matches
var one = await service.SelectSingle(x => x.Email == email);
var byCode = await service.GetByCode("CUS-A7K2M9Q4XZ");
var search = await service.Search("John");
var count = await service.Count(x => x.Status == CustomerStatus.Active);
```

Every persisted string property is generic-searchable unless `[NotSearchable]` or `[DefaultOnReturn]` is applied. `TenantCode` participates only when `options.Search.IncludeTenantCode = true`; tenant isolation remains independent and enforced by providers.

`SearchParam` supports filters, projections, order and join metadata. SQL Server implements common query translation; MongoDB implements provider-appropriate filtering and bulk operations.

## Debug query

Execution remains parameterized. For debugging:

```csharp
var debug = service.GenerateDebugQuery(search);
```

SQL Server returns readable SQL with values embedded for inspection. MongoDB returns readable provider query context. Debug output is not used for execution.

## Registration

```csharp
builder.Services.AddSimpleOrmCore(options =>
{
    options.Database = DatabaseType.SqlServer;

    options.Connection.Host = "localhost";
    options.Connection.Port = 1433;
    options.Connection.Database = "CommerceDB";
    options.Connection.Username = "sa";
    options.Connection.Password = configuration["Database:Password"];

    options.DefaultStringLength = 50;
    options.EnumStorage = EnumStorage.String;

    options.CodeGeneration.Length = 10;

    options.Batch.Save = 100;
    options.Batch.Select = 100;

    options.AutoMigration = true;

    options.MultiTenancy.Enabled = true;
    options.MultiTenancy.JwtClaim = "tenant";

    options.AuditTrail.Enabled = true;
    options.ErrorLog.Enabled = false;
}, typeof(Customer).Assembly);

builder.Services.AddSimpleOrmSqlServer();
// or: builder.Services.AddSimpleOrmMongoDB();

builder.Services.AddSimpleOrmAspNetCore();
```

## SQL Server migration

SQL Server startup synchronization creates missing tables, columns and unique indexes. SimpleORM-managed unique indexes use `SORM_...` names. Destructive schema changes are intentionally gated by configuration.

## MongoDB startup

MongoDB does not perform relational-style property migrations. Startup synchronization is limited to SimpleORM-owned indexes. Old document properties are not automatically rewritten.

## Multi-tenancy

`SimpleORM.Net.AspNetCore` resolves `TenantCode` from JWT. Save assigns the authenticated tenant, and providers scope reads/updates/deletes by tenant when enabled.

## Optional encryption

```csharp
builder.Services.AddSimpleOrmAspNetCore(options =>
{
    options.Encryption.Enabled = true;
    options.Encryption.PublicKey = configuration["Encryption:PublicKey"];
    options.Encryption.PrivateKey = configuration["Encryption:PrivateKey"];
});

app.UseSimpleOrmEncryption();
```

Encrypted requests use `X-SimpleORM-Encrypted: true`. The implementation uses RSA-OAEP for AES key wrapping and AES for request/response bodies.

## Optional error middleware

```csharp
options.ErrorLog.Enabled = true;
app.UseSimpleOrmErrors();
```

It captures trace, URL, HTTP method and optional payload information through normal application logging. The system model `DBErrorLog` is included for DB persistence integration; transaction-failure error persistence should occur outside the failed transaction.

## HTTP wrapper

```csharp
var result = await http.Send<LoginResponse>(new HttpRequestOptions
{
    Url = "https://example.com/login",
    Method = HttpMethodType.Post,
    BodyType = HttpBodyType.Json,
    Payload = new { Username = "john", Password = "secret" },
    Headers = { ["X-App"] = "Commerce" }
});
```

Supports method, headers, query string, body type, payload, bearer token, timeout, typed response and dynamic response.

## XML documentation enforcement

`Directory.Build.props` enables XML documentation and treats `CS1591` as an error. Any undocumented public API should fail the build.

## Build / test / package

```bash
dotnet restore SimpleORM.Net.slnx
dotnet build SimpleORM.Net.slnx -c Release
dotnet test SimpleORM.Net.slnx -c Release

dotnet pack src/SimpleORM.Net/SimpleORM.Net.csproj -c Release
dotnet pack src/SimpleORM.Net.SqlServer/SimpleORM.Net.SqlServer.csproj -c Release
dotnet pack src/SimpleORM.Net.MongoDB/SimpleORM.Net.MongoDB.csproj -c Release
dotnet pack src/SimpleORM.Net.AspNetCore/SimpleORM.Net.AspNetCore.csproj -c Release
dotnet pack src/SimpleORM.Net.Http/SimpleORM.Net.Http.csproj -c Release
```

## Future data transfer phase

The provider boundary is intentionally kept neutral so later data movement can be implemented as:

`source provider -> neutral model/metadata representation -> target provider`

rather than writing separate SQLServer-to-Mongo, Mongo-to-Postgres, MySQL-to-Cassandra combinations.

## License

MIT © 2026 Akintunde Morakinyo.

## End-to-end sample API

A complete Controller → Service example covering every `IDataService<Customer>` method, batching, search, debug-query generation, dynamic extensions, automatic/explicit extension publishing, and extension-value persistence is available in:

`/samples/SimpleORM.Net.Sample.Api/README.md`
