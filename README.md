# maichess-database-service

See `CLAUDE.md` for architecture, contracts, and design notes.

## Mutation Testing (Stryker.NET)

Stryker is installed as a local .NET tool. Configuration lives in
`MaichessDatabaseService.Tests/stryker-config.json`. Adapter implementations
(`Adapters/Postgres`, `Adapters/Mongo`) are excluded because they require a
live database — only the domain and gRPC layers are mutated.

```powershell
# First time on a clean checkout — restore the local tool
dotnet tool restore

# Run mutation tests (from the test project directory)
cd MaichessDatabaseService.Tests
dotnet stryker
```

After the run, open `StrykerOutput/<timestamp>/reports/mutation-report.html`
in a browser to inspect surviving mutants.

To bump the Stryker version: `dotnet tool update dotnet-stryker`.
