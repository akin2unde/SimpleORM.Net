# SimpleORM.Net
[![Build](https://github.com/akin2unde/SimpleORM.Net/actions/workflows/build.yml/badge.svg)](https://github.com/akin2unde/SimpleORM.Net/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SimpleORM.Net.svg)](https://www.nuget.org/packages/SimpleORM.Net)
[![Downloads](https://img.shields.io/nuget/dt/SimpleORM.Net.svg)](https://www.nuget.org/packages/SimpleORM.Net)
[![License](https://img.shields.io/github/license/akin2unde/SimpleORM.Net)](LICENSE)

A lightweight, provider-based ORM for .NET 10 focused on a small repository API, model conventions, batching, transactions, extensions, multi-tenancy, auditing and provider-specific schema management.

**Author:** Akintunde Morakinyo  
**Repository:** `akin2unde/SimpleORM.Net`

## Packages

- `SimpleORM.Net` — core repository, metadata, query model and transaction abstractions.
- `SimpleORM.Net.SqlServer` — SQL Server provider and schema synchronization.
- `SimpleORM.Net.MongoDB` — MongoDB provider and index synchronization.
- `SimpleORM.Net.AspNetCore` — ASP.NET Core tenant/user integration, error middleware and payload encryption.
- `SimpleORM.Net.Http` — typed HTTP request wrapper.


## Quick start

```csharp
builder.Services.AddSimpleOrm(
    options =>
    {
        options.Database = DatabaseType.SqlServer;
        options.Connection.Host = "localhost";
        options.Connection.Port = 1433;
        options.Connection.Database = "CommerceDb";
        options.Connection.Username = "sa";
        options.Connection.Password = configuration["SimpleOrm:Password"];
        options.DefaultStringLength = 50;
        options.CodeGeneration.Length = 10;
        options.Batch.Save = 100;
        options.Batch.Select = 100;
        options.AutoMigration = true;
    },
    typeof(Product).Assembly);

// Provider registration intentionally remains explicit.
builder.Services.AddSimpleOrmSqlServer();
builder.Services.AddSimpleOrmAspNetCore();
```

For MongoDB, set `options.Database = DatabaseType.MongoDb` and call `AddSimpleOrmMongoDB()`.

## Models and code generation

Every persisted model inherits `DBModel`. A table/collection is inferred automatically; `[DBTable]` is only needed to override its database name.

```csharp
public sealed class Product : DBModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public override string GetPrefix() => "PRD";
}
```

Every model instance can generate a code:

```csharp
var product = new Product();
var code = product.GenerateCode();       // PRD-XXXXXXXXXX
var shortCode = product.GenerateCode(6); // PRD-XXXXXX
```

When `Code` is empty during an insert, the repository calls the model's `GenerateCode` using the configured/model metadata length. `Code` is unique by convention.

## Repository API

Inject one repository for all models:

```csharp
public sealed class ProductService(IDataRepository repository)
{
    public Task<Product?> Get(string code, CancellationToken ct) =>
        repository.GetByCode<Product>(code, ct);
}
```

### Select

```csharp
var page = await repository.Select<Product>(
    skip: 0,
    limit: 100,
    cancellationToken: ct,
    batch: 100);

var active = await repository.Select<Product>(
    x => x.Active,
    skip: 0,
    limit: 100,
    cancellationToken: ct);
```

`limit: 0` means fetch all matching records. Physical batches are capped internally at 500.

For joins, selected fields, ordering and richer filtering, pass `SearchParam` or combine it with an expression.

### SelectSingle

```csharp
var product = await repository.SelectSingle<Product>(
    x => x.Code == code,
    ct);
```

### Search

Strings are searchable by convention unless `[NotSearchable]` is applied.

```csharp
var result = await repository.Search<Product>(
    "milk",
    cancellationToken: ct);
```

### Count

```csharp
long total = await repository.Count<Product>(cancellationToken: ct);
long active = await repository.Count<Product>(x => x.Active, cancellationToken: ct);
```

### Save and DataState

One `Save` API handles insert, update and delete through `DataState`.

```csharp
product.DataState = DataState.New;
await repository.Save(product, ct);

product.Price = 2500;
product.DataState = DataState.Changed;
await repository.Save(product, ct);

product.DataState = DataState.Removed;
await repository.Save(product, ct);
```

Models use soft delete by default. Apply `[HardDelete]` to models that must be physically deleted.

Batch save places `CancellationToken` before the optional batch parameter:

```csharp
await repository.Save(products, ct, batch: 200);
```

## Transactions

Normal saves manage their transaction automatically. Use `IDBTransactionManager.Execute` when several repository operations must commit or roll back together.

```csharp
return await transactionManager.Execute<IReadOnlyList<Product>>(
    async () =>
    {
        var savedProducts = await repository.Save(products, ct);

        if (inventories.Count > 0)
        {
            await repository.Save(inventories, ct);
        }

        return savedProducts;
    },
    ct);
```

Nested repository calls reuse the current scoped transaction; they do not independently commit it.

## SearchParam, joins and selected fields

`SearchParam` supports filters, ordering, join type and selected fields. Expression filters can be used alone or merged with a `SearchParam`.

```csharp
var result = await repository.Select<Order>(
    x => x.Total > 1000,
    searchParam,
    skip: 0,
    limit: 100,
    cancellationToken: ct);
```

## Debug queries

Generate a provider-specific query with values embedded for debugging only:

```csharp
var query = repository.GenerateDebugQuery<Product>(searchParam);
```

The generated text is for inspection and is never used as the execution path.

## Extensions

Apply `[Extendable]` to models that support dynamic extension definitions. `DBModel.Extended` contains loaded extension values. Definitions describe field code/name, data type, required state, size and default value.

`options.Extensions.RequirePublish = false` makes saved definitions immediately available. Set it to `true` to require explicit publishing.

The sample API contains end-to-end definition, publishing, loading and saving examples.

## Ignored properties

Use `[Ignore]` for model properties that belong to runtime/application state but must never be persisted:

```csharp
public sealed class Country : DBModel
{
    public string Name { get; set; } = string.Empty;

    [Ignore]
    public string? DisplayLabel { get; set; }
}
```

Ignored properties are excluded from SQL Server schema generation, selects, inserts, updates, filters, joins, and ordering. MongoDB also omits ignored members from BSON persistence. Existing ignored SQL columns are only physically removed when destructive migrations are enabled.

## Multi-tenancy

Enable tenant filtering globally:

```csharp
options.MultiTenancy.Enabled = true;
options.MultiTenancy.JwtClaim = "tenant";
```

ASP.NET Core can resolve the tenant from the configured JWT claim. Tenant behavior remains optional.

Use `[Global]` for shared models that must not require or persist a tenant even when application multi-tenancy is enabled:

```csharp
[Global]
public sealed class Country : DBModel
{
    public string Name { get; set; } = string.Empty;
}
```

Normal models remain tenant scoped. Global models skip tenant filters on read/update/delete and the inherited `Tenant` property is excluded from persistence. Typical uses include tenant records themselves and shared reference data such as countries.

## Audit and error logging

Audit trails are opt-in globally and can be disabled per model. Error logging middleware is also optional and can persist useful failure context such as request URL and payload information where available.

Stale error logs can be physically removed on a UTC cron schedule:

```csharp
options.ErrorLog.Enabled = true;
options.ErrorLog.AutoDeleteEnabled = true;
options.ErrorLog.RetentionDays = 60;
options.ErrorLog.CleanupCron = "0 0 1 */3 *"; // every quarter
```

The cleanup above runs every three months and deletes error-log records whose `CreatedAt` is older than 60 days. A monthly schedule can use `0 0 1 * *`. Cleanup bypasses tenant scoping because retention is system-level maintenance.

Any model can opt into the same retention mechanism:

```csharp
[AutoDelete(60, "0 0 1 * *")]
public sealed class TemporaryImport : DBModel
{
}
```


## Enum and string conventions

```csharp
options.DefaultStringLength = 50;
options.EnumStorage = EnumStorage.String;
```

Individual model attributes can override supported conventions. Password/sensitive return values can use `[DefaultOnReturn]` so their values are reset after materialization.

## Schema management

SQL Server auto-migration synchronizes supported table, column, key and index changes and records migration failures. MongoDB intentionally avoids relational-style migrations and synchronizes indexes instead.

## Raw provider queries

`IDBQuery` is available for advanced provider-specific direct queries and supports dynamic or typed result shapes. Prefer `IDataRepository` for normal application CRUD.

## Sample project

`samples/SimpleORM.Net.Sample.Api` demonstrates:

- Controller → application service → `IDataRepository`
- expression and `SearchParam` selects
- `SelectSingle`, `GetByCode`, search and count
- single and batch saves
- `DataState`
- transactions across multiple model types
- extensions and publishing
- debug query generation
- SQL Server/MongoDB provider selection
- ASP.NET Core integration

## Build and test

```bash
dotnet restore SimpleORM.Net.slnx
dotnet build SimpleORM.Net.slnx -c Release
dotnet test SimpleORM.Net.slnx -c Release
```

XML documentation and warnings-as-errors are enabled repository-wide.

## NuGet publishing

GitHub Actions includes:

- `.github/workflows/build.yml` — restore, build and test pushes/PRs.
- `.github/workflows/nuget.yml` — build, test, pack and publish tags matching `v*`.

Create a GitHub Actions secret named `NUGET_API_KEY`, then push a version tag such as `v0.1.0` to run the publishing workflow.

## Roadmap

Future phases are intended to add more providers and provider-neutral data movement between database types without changing application models or the repository CRUD API.

## License

MIT. See `LICENSE`.
