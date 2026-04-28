using MaichessDatabaseService.Adapters.Postgres;
using MaichessDatabaseService.Tests.Support;
using Npgsql;
using Xunit;

namespace MaichessDatabaseService.Tests.Integration;

public sealed class PostgresRecordRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string? connectionString;
    private PostgresRecordRepository? repo;
    private readonly string table;

    public PostgresRecordRepositoryIntegrationTests()
    {
        connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        table = "test_" + Guid.NewGuid().ToString("N");
    }

    public async Task InitializeAsync()
    {
        if (connectionString is null)
        {
            return;
        }

        repo = new PostgresRecordRepository(connectionString);
        await using NpgsqlDataSource ds = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlCommand cmd = ds.CreateCommand(
            $"CREATE TABLE \"{table}\" (id UUID PRIMARY KEY, color TEXT, type TEXT)");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null)
        {
            return;
        }

        await using NpgsqlDataSource ds = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlCommand cmd = ds.CreateCommand($"DROP TABLE IF EXISTS \"{table}\"");
        await cmd.ExecuteNonQueryAsync();
    }

    [RequiresEnvVarFact("POSTGRES_CONNECTION_STRING")]
    public async Task DeleteWhereAsync_MatchingFilter_DeletesOnlyMatchingRecords()
    {
        PostgresRecordRepository r = repo!;

        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "red" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "red" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "blue" }, default);

        long deleted = await r.DeleteWhereAsync(
            table,
            new Dictionary<string, object?> { ["color"] = "red" },
            default);

        Assert.Equal(2, deleted);

        var remaining = await r.ListAsync(table, new Dictionary<string, object?>(), 0, 0, default);
        Assert.Single(remaining);
        Assert.Equal("blue", remaining[0].Fields["color"]);
    }

    [RequiresEnvVarFact("POSTGRES_CONNECTION_STRING")]
    public async Task DeleteWhereAsync_EmptyFilter_DeletesAllRecords()
    {
        PostgresRecordRepository r = repo!;

        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "a" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "b" }, default);

        long deleted = await r.DeleteWhereAsync(table, new Dictionary<string, object?>(), default);

        Assert.Equal(2, deleted);

        var remaining = await r.ListAsync(table, new Dictionary<string, object?>(), 0, 0, default);
        Assert.Empty(remaining);
    }

    [RequiresEnvVarFact("POSTGRES_CONNECTION_STRING")]
    public async Task CountAsync_MatchingFilter_ReturnsCorrectCount()
    {
        PostgresRecordRepository r = repo!;

        await r.InsertAsync(table, new Dictionary<string, object?> { ["type"] = "widget" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["type"] = "widget" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["type"] = "gadget" }, default);

        long count = await r.CountAsync(
            table,
            new Dictionary<string, object?> { ["type"] = "widget" },
            default);

        Assert.Equal(2, count);
    }

    [RequiresEnvVarFact("POSTGRES_CONNECTION_STRING")]
    public async Task CountAsync_EmptyFilter_ReturnsAllRecords()
    {
        PostgresRecordRepository r = repo!;

        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "x" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "y" }, default);
        await r.InsertAsync(table, new Dictionary<string, object?> { ["color"] = "z" }, default);

        long count = await r.CountAsync(table, new Dictionary<string, object?>(), default);

        Assert.Equal(3, count);
    }

    [RequiresEnvVarFact("POSTGRES_CONNECTION_STRING")]
    public async Task CountAsync_EmptyCollection_ReturnsZero()
    {
        PostgresRecordRepository r = repo!;

        long count = await r.CountAsync(table, new Dictionary<string, object?>(), default);

        Assert.Equal(0, count);
    }
}
