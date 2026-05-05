using System.Diagnostics.CodeAnalysis;
using MaichessDatabaseService.Domain;
using Npgsql;

namespace MaichessDatabaseService.Adapters.Postgres.Migrations;

[ExcludeFromCodeCoverage]
internal sealed class UserPostgresMigration : IMigration
{
    private readonly string connectionString;

    public UserPostgresMigration(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public string Domain => "user";

    public async Task RunAsync(CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(ct);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "users" (
                "id"            UUID    NOT NULL PRIMARY KEY,
                "username"      TEXT    NOT NULL UNIQUE,
                "password_hash" TEXT    NOT NULL,
                "elo"           INTEGER NOT NULL DEFAULT 1200,
                "wins"          INTEGER NOT NULL DEFAULT 0,
                "losses"        INTEGER NOT NULL DEFAULT 0,
                "draws"         INTEGER NOT NULL DEFAULT 0
            )
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
