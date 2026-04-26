# Database Service

Provides database access as a gRPC service for a single configured domain. Callers interact through a generic CRUD interface; all DB-specific code is confined to swappable adapter implementations. Two instances are deployed:

- `user-db` — PostgreSQL, serving User and Auth. The Auth service is restricted to read-only operations via instance configuration.
- `match-db` — MongoDB, serving Match Manager.

## Contracts

- **gRPC:** `maichess-api-contracts/protos/database-service/v1/database.proto` (once added)
- **Generated stubs:** reference `Maichess.PlatformProtos` (see `maichess-api-contracts/dotnet/`)

Implement against these contracts exactly. If a contract cannot be implemented as specified, document the blocker in `CONTRACT_NOTES.md` — do not silently deviate.

## Stack

- **Runtime:** ASP.NET (net10.0), C#, nullable enabled
- **RPC:** gRPC server
- **Adapters (loaded at startup by config):**
  - PostgreSQL via Npgsql / EF Core
  - MongoDB via the official MongoDB .NET Driver

## Architecture — Layered, DB-Agnostic

The central constraint is that **only adapter implementations may contain DB-specific code**. Every other layer is database-agnostic and must not reference any DB driver, ORM type, or connection API.

```
MaichessDatabaseService/
  Grpc/            # gRPC service implementations — translate proto ↔ domain; call IRecordRepository
  Domain/          # Domain models and IRecordRepository interface — zero DB imports
  Adapters/
    Postgres/      # EF Core implementation of IRecordRepository
    Mongo/         # MongoDB driver implementation of IRecordRepository
  Program.cs       # Startup: reads config, selects adapter, wires DI
```

### Domain layer (`Domain/`)

Defines the contracts the rest of the service depends on. Must not import any DB driver or ORM namespace.

- **Models:** plain C# records representing the data entities for the configured domain.
- **`IRecordRepository`:** the single interface all other layers program against. Exposes generic CRUD operations (get by id, list with optional filters, insert, update, delete). The interface is typed to the domain model — not to proto types, not to DB types.

### gRPC layer (`Grpc/`)

Maps incoming proto requests to `IRecordRepository` calls and maps results back to proto responses. Must not contain business logic, query construction, or any DB-specific code. Validate inputs here; trust results from the repository.

### Adapter layer (`Adapters/`)

Implements `IRecordRepository` for a specific database. This is the **only** place permitted to import or reference:
- `Microsoft.EntityFrameworkCore` or Npgsql types (Postgres adapter)
- `MongoDB.Driver` types (Mongo adapter)

Each adapter translates domain model operations into DB-specific queries. No adapter type should be visible outside its own directory — all cross-layer dependencies are on the `IRecordRepository` interface.

### Startup (`Program.cs`)

Reads the `Database:Adapter` config key (`"postgres"` or `"mongo"`) and registers the matching adapter as the `IRecordRepository` implementation. Also reads `Database:ReadOnly` (`true`/`false`) and — when true — wraps the adapter in a decorator that rejects all write operations with `StatusCode.PermissionDenied`.

## Configuration

```json
{
  "Database": {
    "Adapter": "postgres",
    "ConnectionString": "...",
    "ReadOnly": false
  }
}
```

Instance-level read-only enforcement lives in the decorator, not in the adapter or gRPC layer.

## Code Style

- One concern per class; keep classes small
- No dead code, no commented-out blocks, no TODOs left in merged code
- Use C# records for domain models and DTOs
- Validate inputs at gRPC boundaries; trust internal data after that point
- No comments unless explaining a non-obvious algorithm — names carry intent
- Never let a DB type or ORM type escape its adapter

## Tests

- Do not change tests to make them pass — only change tests when the requirement they cover changes.
- Unit-test the gRPC layer against a mock `IRecordRepository` — no DB involved.
- Integration-test each adapter against a real database instance.
