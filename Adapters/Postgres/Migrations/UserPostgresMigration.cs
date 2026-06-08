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

        // Column defaults mirror the user service's seeds (see Rating/Glicko2.cs):
        // rating 400, rating_deviation 350, volatility 0.06, dev_mode false.
        // The CREATE covers fresh installs; the ALTERs backfill tables that
        // predate the dev-mode (feature 01) and Glicko-2 (feature 03) fields.
        // All statements are idempotent so the migration is safe to re-run.
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "users" (
                "id"               UUID             NOT NULL PRIMARY KEY,
                "username"         TEXT             NOT NULL UNIQUE,
                "password_hash"    TEXT             NOT NULL,
                "elo"              INTEGER          NOT NULL DEFAULT 1200,
                "wins"             INTEGER          NOT NULL DEFAULT 0,
                "losses"          INTEGER          NOT NULL DEFAULT 0,
                "draws"            INTEGER          NOT NULL DEFAULT 0,
                "dev_mode"         BOOLEAN          NOT NULL DEFAULT FALSE,
                "rating"           DOUBLE PRECISION NOT NULL DEFAULT 400,
                "rating_deviation" DOUBLE PRECISION NOT NULL DEFAULT 350,
                "volatility"       DOUBLE PRECISION NOT NULL DEFAULT 0.06
            );

            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "dev_mode"         BOOLEAN          NOT NULL DEFAULT FALSE;
            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "rating"           DOUBLE PRECISION;
            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "rating_deviation" DOUBLE PRECISION NOT NULL DEFAULT 350;
            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "volatility"       DOUBLE PRECISION NOT NULL DEFAULT 0.06;

            -- Seed rating from the existing display elo for rows created before the
            -- rating column existed, matching the user service's rating <- elo fallback.
            UPDATE "users" SET "rating" = "elo" WHERE "rating" IS NULL;
            ALTER TABLE "users" ALTER COLUMN "rating" SET DEFAULT 400;
            ALTER TABLE "users" ALTER COLUMN "rating" SET NOT NULL;

            -- Debezium CDC (feature-prompts/10, change-data-capture.md): emit the full
            -- old row on UPDATE/DELETE so the CDC->user.events transform can tell which
            -- fields changed (profile vs rating) per operation. Without FULL, Postgres
            -- logical replication ships only the primary key in the before-image. The
            -- users table is low-write, so the extra WAL volume is negligible.
            ALTER TABLE "users" REPLICA IDENTITY FULL;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
