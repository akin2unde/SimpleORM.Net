# Changelog

## Unreleased - optimistic concurrency

- Added ORM-managed `DBModel.Version` optimistic concurrency token.
- Added `SimpleOrmOptions.Concurrency.Enabled`, enabled by default.
- Added `[DisableConcurrencyCheck]` for models that intentionally allow last-write-wins updates.
- Added `DBConcurrencyException` for stale update/delete detection.
- SQL Server checks versions inside the existing bulk staging join and increments the version atomically.
- MongoDB checks versions in bulk mutation filters and increments/replaces the version atomically.
- SQL auto-migration seeds `Version = 1` when adding the required version column to populated tables.
- Older MongoDB documents without `Version` are treated as version `1` for their first protected mutation.
- ASP.NET Core error middleware maps `DBConcurrencyException` to HTTP 409 Conflict.


## Unreleased

- Added provider-neutral `SearchParam` value normalization so JSON values, including numeric or named enums and multi-value operators, are converted to model CLR types before reaching any database provider.
- Fixed MongoDB dynamic projections and raw dynamic queries returning `Decimal128` values as `{}` by converting representable values to CLR `decimal` before serialization.
- Applied model-level and property-level `EnumStorage` metadata to MongoDB inserts, replacements, and search filters instead of relying on the driver's default numeric enum serialization.
- Reworked SQL Server batch writes around `SqlBulkCopy` and set-based staging updates/deletes.
- Fixed no-filter `Select<T>()` translation failure.
- Enabled built-in automatic `DBModel.Extended` persistence.
- Added MongoDB operator parity, typed raw-query support, raw execute operations, and Inner/LeftOuter dynamic joins.
- Restored the paging convenience overload `Select<T>(int skip, int limit = 100, ...)` without reintroducing the constant-true expression path.
- Added end-to-end `SelectDynamic` coverage to the sample service, controller, and README.


## Sample API correction

- Rebuilt the sample around Controller -> ICustomerService -> IDataRepository.
- Added the missing Customer sample model.
- Added end-to-end examples for every IDataRepository method.
- Added database-backed sample extension loading/saving and optional publishing workflow.
- Added a detailed sample README.
- Fixed Mongo provider DI lifetime mismatch with scoped tenant resolution.
- Added composite unique groups to extension definition/value system models.
- Fixed remaining SQL Server schema tuple precision/scale byte conversions.
- Prevented extension writes during Removed DataState processing.


## 0.1.0-v5

- Fixed nullable enum-storage inference in `DBMetadataProvider`.
- Reformatted C# source for readability, including line breaks after statements/semicolons.
- Expanded compressed metadata logic into readable multi-line code.


## 0.1.0
- Initial .NET 10 MVP.
- SQL Server and MongoDB providers.
- DBModel/DataState single Save API.
- Metadata cache and convention-first mapping.
- Random model codes.
- Batching and transaction coordination.
- Select, SelectSingle, Search, Count, GetByCode.
- SQL debug query rendering.
- SQL schema synchronization and Mongo index synchronization.
- ASP.NET JWT tenancy, optional error and encryption middleware.
- Typed outbound HTTP wrapper.
- Tests, sample API, XML-doc enforcement and NuGet metadata.

## 0.1.1 - 2026-08-09

- Separated classes, interfaces, and enums into individual source files.
- Normalized XML documentation placement so declarations are not embedded in documentation comment lines.
- Kept XML documentation generation and CS1591 enforcement enabled.
# Unreleased

- Fixed MongoDB persistence so `[Ignore]` properties are excluded before BSON
  serialization. Ignored nested models are no longer inspected or serialized
  during insert and replacement update operations.
- Extended the MongoDB `[Ignore]` convention to nested value objects and list
  items that do not inherit from `DBModel`, including upload-only properties
  such as `IFormFile` on a product image model.
- MongoDB persisted-document creation now respects custom
  `[DBColumn(Name = "...")]` names without first serializing the complete model.
