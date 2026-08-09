# SimpleORM.Net Sample API

This project is an end-to-end example of using **SimpleORM.Net** from an ASP.NET Core application with a normal **Controller → Application Service → IDataService<T>** structure.

The controller does not know how the ORM works internally. `CustomerController` talks to `ICustomerService`; `CustomerService` uses `IDataService<Customer>` and `IDataService<DBExtensionDefinition>`.

The sample also replaces the core no-op `IExtensionService` with `SampleExtensionService` so dynamic Customer extension values are actually persisted and loaded using the currently selected database provider.

## 1. Select a provider

Edit `appsettings.json`.

SQL Server:

```json
{
  "SimpleOrm": {
    "Provider": "SqlServer",
    "Host": "localhost",
    "Port": 1433,
    "Database": "SimpleOrmSample",
    "Username": "sa",
    "Password": "CHANGE_ME"
  }
}
```

MongoDB:

```json
{
  "SimpleOrm": {
    "Provider": "MongoDb",
    "Host": "localhost",
    "Port": 27017,
    "Database": "SimpleOrmSample",
    "Username": null,
    "Password": null
  }
}
```

The rest of the application code is unchanged when switching providers.

## 2. Model

`Customer` inherits from `DBModel`, so it is automatically persisted. `[Extendable]` enables dynamic extension values.

```csharp
[Extendable]
public sealed class Customer : DBModel
{
    public string Name { get; set; } = string.Empty;

    [Unique]
    [DBColumn(Size = 150)]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    [DefaultOnReturn]
    [NotSearchable]
    public string Password { get; set; } = string.Empty;

    public CustomerStatus Status { get; set; }
}
```

By convention:

- table/collection name is `Customer`;
- `Code` is generated automatically when empty;
- generated code uses the model prefix plus a random suffix, for example `CUS-A7K4M2X9PQ`;
- `Code` is unique;
- normal string properties are searchable unless `[NotSearchable]` is used;
- `Password` is reset before normal query results are returned because of `[DefaultOnReturn]`;
- delete is soft-delete unless the model has `[HardDelete]`.

## 3. Registration

`Program.cs` calls `AddSimpleOrmCore`, then registers either SQL Server or MongoDB.

```csharp
builder.Services.AddSimpleOrmCore(
    options =>
    {
        options.Database = provider;
        options.Connection.Host = "localhost";
        options.Connection.Port = 1433;
        options.Connection.Database = "SimpleOrmSample";
        options.Connection.Username = "sa";
        options.Connection.Password = "CHANGE_ME";

        options.DefaultStringLength = 50;
        options.EnumStorage = EnumStorage.String;
        options.CodeGeneration.Length = 10;
        options.Batch.Save = 100;
        options.Batch.Select = 100;
        options.AutoMigration = true;
    },
    typeof(Customer).Assembly,
    typeof(DBExtensionDefinition).Assembly);
```

Provider registration:

```csharp
builder.Services.AddSimpleOrmSqlServer();
```

or:

```csharp
builder.Services.AddSimpleOrmMongoDB();
```

The sample then registers its application service and database-backed extension adapter:

```csharp
builder.Services.AddScoped<IExtensionService, SampleExtensionService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
```

## 4. Controller → service pattern

The controller injects only the application service:

```csharp
public sealed class CustomerController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomerController(ICustomerService service)
    {
        _service = service;
    }
}
```

The application service injects the generic ORM services:

```csharp
public CustomerService(
    IDataService<Customer> customers,
    IDataService<DBExtensionDefinition> extensionDefinitions,
    SimpleOrmOptions options)
```

This keeps HTTP concerns out of the data layer and keeps ORM-specific calls out of controllers.

## 5. IDataService methods demonstrated by CustomerService

### Select with SearchParam

```csharp
var search = new SearchParam();
search.Filters.Add(
    new SearchFilter
    {
        Field = nameof(Customer.Status),
        Operator = SearchOperator.Equal,
        Value = CustomerStatus.Active
    });

var result = await _customers.Select(
    search,
    skip: 0,
    limit: 100,
    batch: 100,
    cancellationToken);
```

`limit: 0` means return all matching records. Internally the ORM still reads in batches and never sends a physical batch larger than 500.

### Select with expression

```csharp
var result = await _customers.Select(
    customer => customer.Status == CustomerStatus.Active,
    search: null,
    skip: 0,
    limit: 100,
    batch: null,
    cancellationToken);
```

### SelectSingle with SearchParam

```csharp
var search = new SearchParam();
search.Filters.Add(
    new SearchFilter
    {
        Field = nameof(Customer.Email),
        Operator = SearchOperator.Equal,
        Value = "user@example.com"
    });

var customer = await _customers.SelectSingle(
    search,
    cancellationToken);
```

### SelectSingle with expression

```csharp
var customer = await _customers.SelectSingle(
    item => item.Email == "user@example.com",
    search: null,
    cancellationToken);
```

### GetByCode

```csharp
var customer = await _customers.GetByCode(
    "CUS-A7K4M2X9PQ",
    cancellationToken);
```

### Generic string search

```csharp
var result = await _customers.Search(
    "Akintunde",
    searchParam: null,
    skip: 0,
    limit: 100,
    batch: null,
    cancellationToken);
```

All eligible string fields are searched automatically. `[NotSearchable]` excludes a field. `TenantCode` participates only when `Search.IncludeTenantCode` is enabled.

### Count with SearchParam

```csharp
var total = await _customers.Count(
    search,
    cancellationToken);
```

The result type is `long`.

### Count with expression

```csharp
var total = await _customers.Count(
    customer => customer.Status == CustomerStatus.Active,
    search: null,
    cancellationToken);
```

### Save one model

`DataState` decides what persistence operation occurs.

Insert:

```csharp
var customer = new Customer
{
    Name = "ABC Limited",
    Email = "info@abc.test",
    Status = CustomerStatus.Active,
    DataState = DataState.New
};

await _customers.Save(customer, cancellationToken);
```

Update:

```csharp
customer.Name = "ABC Nigeria Limited";
customer.DataState = DataState.Changed;
await _customers.Save(customer, cancellationToken);
```

Delete:

```csharp
customer.DataState = DataState.Removed;
await _customers.Save(customer, cancellationToken);
```

Normal models are soft-deleted. `[HardDelete]` changes `Removed` to a physical delete.

### Save a batch

```csharp
await _customers.Save(
    customers,
    batch: 250,
    cancellationToken);
```

The configured/default batch is 100. A supplied or configured batch above 500 is physically capped at 500. The logical Save still processes the complete collection.

### GenerateDebugQuery

```csharp
var query = _customers.GenerateDebugQuery(
    search,
    skip: 0,
    limit: 100);
```

SQL Server returns SQL with debug values embedded. MongoDB returns its provider-specific readable query representation. The generated debug query is for inspection only and is not used for execution.

## 6. Dynamic extensions

The sample Customer model is `[Extendable]`.

An extension has two parts:

1. a definition stored in `__DBExtensionDefinition`;
2. a record value stored in `__DBExtensionValue`.

### Create an extension definition

POST `/customer/Extension/Definition`:

```json
{
  "fieldCode": "CREDIT_LIMIT",
  "fieldName": "Credit Limit",
  "dataType": 3,
  "required": false,
  "size": null,
  "defaultValue": null,
  "dataState": 1
}
```

The service automatically sets:

```csharp
definition.ModelName = nameof(Customer);
```

When:

```csharp
options.Extensions.RequirePublish = false;
```

saving the definition also sets:

```csharp
definition.Published = true;
```

so it is immediately available.

### Manual publishing

Enable:

```json
{
  "SimpleOrm": {
    "Extensions": {
      "RequirePublish": true
    }
  }
}
```

A newly saved definition can remain unpublished. Publish it with:

```http
POST /customer/Extension/Publish/{definitionCode}
```

The service loads the definition by its ORM `Code`, changes `Published` to `true`, sets `DataState.Changed`, and saves it.

### Load extension values

Normal Customer reads automatically call `IExtensionService.Load`. `SampleExtensionService` loads all active Customer extension definitions and values in batches, then fills:

```csharp
customer.Extended
```

Example returned structure:

```json
{
  "code": "CUS-A7K4M2X9PQ",
  "name": "ABC Limited",
  "extended": {
    "CREDIT_LIMIT": {
      "code": "CREDIT_LIMIT",
      "name": "Credit Limit",
      "dataType": 3,
      "required": false,
      "size": null,
      "data": 500000
    }
  }
}
```

Definitions without a stored value are still returned with `data` equal to the definition default or `null`.

### Save an extension value

POST:

```http
POST /customer/{customerCode}/Extension/CREDIT_LIMIT
```

body:

```json
500000
```

The service:

1. loads the Customer;
2. verifies `CREDIT_LIMIT` is defined/published;
3. sets `customer.Extended["CREDIT_LIMIT"].Data`;
4. changes the Customer to `DataState.Changed`;
5. calls `IDataService<Customer>.Save`;
6. `SampleExtensionService.Save` writes the extension value using the same business transaction.

Required extension fields are validated before their values are written. String extensions also enforce the definition size when supplied.

## 7. SearchParam examples

Contains:

```csharp
search.Filters.Add(
    new SearchFilter
    {
        Field = nameof(Customer.Name),
        Operator = SearchOperator.Contains,
        Value = "ABC"
    });
```

IN:

```csharp
search.Filters.Add(
    new SearchFilter
    {
        Field = nameof(Customer.Status),
        Operator = SearchOperator.In,
        Value = new[]
        {
            CustomerStatus.Active,
            CustomerStatus.Suspended
        }
    });
```

Between:

```csharp
search.Filters.Add(
    new SearchFilter
    {
        Field = nameof(Customer.CreatedAt),
        Operator = SearchOperator.Between,
        Value = new[]
        {
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow
        }
    });
```

Projection:

```csharp
search.Fields.Add(nameof(Customer.Code));
search.Fields.Add(nameof(Customer.Name));
search.Fields.Add(nameof(Customer.Email));
```

Ordering:

```csharp
search.OrderBy.Add(
    new SearchOrder
    {
        Field = nameof(Customer.Name),
        Descending = false
    });
```

## 8. Startup schema handling

The sample discovers `DBModel` classes and registers their metadata before calling:

```csharp
await synchronizer.Synchronize(cancellationToken);
```

For SQL Server this performs relational schema synchronization according to the configured migration rules.

For MongoDB it synchronizes ORM-managed indexes rather than attempting relational-style column migration.

## 9. Multi-tenancy

Enable:

```json
{
  "SimpleOrm": {
    "MultiTenancy": {
      "Enabled": true,
      "JwtClaim": "tenant"
    }
  }
}
```

`AddSimpleOrmAspNetCore()` supplies JWT-backed tenant/user providers. When tenancy is enabled, the provider automatically scopes reads and writes to the current tenant. Making `TenantCode` searchable does not disable tenant isolation.

## 10. Suggested first run

1. Set the connection values in `appsettings.json`.
2. Start SQL Server or MongoDB.
3. Run the API.
4. Save a Customer with `DataState.New`.
5. Fetch it with `GetByCode`.
6. Search for it with `/customer/Search`.
7. Create a `CREDIT_LIMIT` extension definition.
8. Fetch the Customer again and confirm the definition appears under `Extended`.
9. Set the Customer's `CREDIT_LIMIT` value.
10. Fetch the Customer again and confirm the typed extension value is returned.
11. Use `/customer/GenerateDebugQuery` to inspect the generated provider query.

This sample intentionally keeps the HTTP layer thin and shows the ORM from an application-service layer, which is the recommended pattern for larger applications.
