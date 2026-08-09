# Changelog

## Sample API correction

- Rebuilt the sample around Controller -> ICustomerService -> IDataService<Customer>.
- Added the missing Customer sample model.
- Added end-to-end examples for every IDataService<Customer> method.
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
