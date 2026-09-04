# Performance and correctness pass — 2026-09-04

This pass responds to the Docker correctness and 1,000,000-record benchmark run completed on 4 September 2026.

## Why SQL Server writes were much slower

The benchmark used a write batch of 50 for SQL Server and 500 for MongoDB. More importantly, the SQL update/delete implementation still executed one database command per model inside the transaction, while MongoDB used bulk-write operations. That made network/command setup and SQL execution overhead dominate the SQL run.

The recorded create throughput was 3,471 records/second for SQL Server versus 53,573 records/second for MongoDB. Update throughput was 6,067 versus 20,284 records/second, and soft-delete throughput was 7,675 versus 23,287 records/second.

## SQL Server write-path changes

- Inserts now use `SqlBulkCopy` for each SimpleORM batch.
- Updates stage the batch into a temporary SQL table with `SqlBulkCopy`, then execute one set-based `UPDATE ... FROM` statement.
- Hard deletes stage keys and execute one joined `DELETE` statement.
- Soft deletes stage keys/audit values and execute one set-based `UPDATE` statement.
- Tenant keys remain part of update/delete matching for tenant-scoped models.
- Soft-deleted rows remain protected from ordinary updates.
- The previous parameter-count bottleneck is removed from the bulk write path, so a SQL batch size of 500 can be tested fairly against MongoDB.

These changes retain the existing SimpleORM transaction boundary. They reduce command round-trips but do not guarantee SQL Server and MongoDB will have identical throughput because their storage, indexing, constraint and durability behavior differs.

## Correctness fixes

- Parameterless `Select<T>` no longer creates a constant `true` expression, so it bypasses expression translation and performs an ordinary no-filter select.
- The default extension service now persists and loads `DBModel.Extended` values instead of acting as a no-op.
- MongoDB now implements GT, GTE, LT, LTE, StartsWith, EndsWith, NotIn, IsNull, IsNotNull, Between and NotBetween.
- MongoDB typed raw query and typed single-query materialization are implemented.
- MongoDB raw execute has an explicit JSON operation contract (`insertOne`, `updateMany`, `deleteMany`, plus a no-op request for capability checks).
- MongoDB `SelectDynamic` supports the tested Inner/LeftOuter `$lookup` join subset. Joined tenant-scoped models are independently tenant-filtered and soft-deleted joined rows are excluded.
- SQL and MongoDB continue to honor `[Ignore]`, `[Global]`, `limit = 0`, and stale-data cleanup behavior from the previous pass.

## Benchmark harness note

The existing benchmark intentionally throws the SQL `Product.Images` compatibility result even though the benchmark model has `[Ignore]` on that collection. The library should continue to require `[Ignore]` (or a separate child-table model) for relational collection properties. The harness should be adjusted before treating that row as a real library failure.

## Next benchmark

For the next performance comparison, run the same workload with SQL Server and MongoDB both using a batch size of 500. Keep the same container CPU/memory/storage, indexes, record payload, transaction scope, and database state. Compare total seconds and records/second for create, update and soft delete.

Also rerun the full correctness suite before interpreting throughput results.
