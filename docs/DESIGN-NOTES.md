# Design notes

The ZIP stays within the design agreed in the architecture discussion.

Low-level implementation mechanics included without introducing new architectural patterns:

1. SQL parameter builders/identifier escaping are internal necessities for parameterized execution and debug-query rendering.
2. The MVP SQL materializer uses a public or non-public parameterless constructor. This is an implementation limit, not a generic `new()` constraint on `IDataRepository`.
3. MongoDB performs index synchronization only, not property/type migrations.
4. XML documentation is enforced with `CS1591` as an error.
5. The sample explicitly runs startup schema/index synchronization so the lifecycle is visible rather than hidden in a hosted service.
6. `IConfigurationDecryptor` is part of the core contract for `ENC:` database-password handling, but no secret-store/key-management implementation is embedded because no specific secret provider was selected during design.
7. The error-log system model is included. ASP.NET middleware currently routes failure details through `ILogger`; a persistent DB sink can use `DBErrorLog` outside failed business transactions without changing the public middleware API.
8. The default extension/audit services are deliberately lightweight placeholders behind interfaces. Shared system models are included so full persisted extension/audit behavior can be advanced without changing the public `IDataRepository` contract.
9. Future cross-database transfer should be built above providers using neutral metadata/record forms, not provider-pair adapters.
