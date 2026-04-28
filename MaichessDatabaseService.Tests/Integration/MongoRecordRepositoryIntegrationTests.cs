using MaichessDatabaseService.Adapters.Mongo;
using MaichessDatabaseService.Tests.Support;
using Xunit;

namespace MaichessDatabaseService.Tests.Integration;

public sealed class MongoRecordRepositoryIntegrationTests : IDisposable
{
    private readonly MongoRecordRepository? repo;
    private readonly string collection;

    public MongoRecordRepositoryIntegrationTests()
    {
        string? cs = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (cs is not null)
        {
            repo = new MongoRecordRepository(cs);
        }

        collection = "test_" + Guid.NewGuid().ToString("N");
    }

    public void Dispose() => repo?.Dispose();

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task DeleteWhereAsync_MatchingFilter_DeletesOnlyMatchingRecords()
    {
        MongoRecordRepository r = repo!;

        await r.InsertAsync(collection, new Dictionary<string, object?> { ["color"] = "red" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["color"] = "red" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["color"] = "blue" }, default);

        long deleted = await r.DeleteWhereAsync(
            collection,
            new Dictionary<string, object?> { ["color"] = "red" },
            default);

        Assert.Equal(2, deleted);

        var remaining = await r.ListAsync(collection, new Dictionary<string, object?>(), 0, 0, default);
        Assert.Single(remaining);
        Assert.Equal("blue", remaining[0].Fields["color"]);
    }

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task DeleteWhereAsync_EmptyFilter_DeletesAllRecords()
    {
        MongoRecordRepository r = repo!;

        await r.InsertAsync(collection, new Dictionary<string, object?> { ["x"] = "a" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["x"] = "b" }, default);

        long deleted = await r.DeleteWhereAsync(collection, new Dictionary<string, object?>(), default);

        Assert.Equal(2, deleted);

        var remaining = await r.ListAsync(collection, new Dictionary<string, object?>(), 0, 0, default);
        Assert.Empty(remaining);
    }

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task CountAsync_MatchingFilter_ReturnsCorrectCount()
    {
        MongoRecordRepository r = repo!;

        await r.InsertAsync(collection, new Dictionary<string, object?> { ["type"] = "widget" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["type"] = "widget" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["type"] = "gadget" }, default);

        long count = await r.CountAsync(
            collection,
            new Dictionary<string, object?> { ["type"] = "widget" },
            default);

        Assert.Equal(2, count);
    }

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task CountAsync_EmptyFilter_ReturnsAllRecords()
    {
        MongoRecordRepository r = repo!;

        await r.InsertAsync(collection, new Dictionary<string, object?> { ["v"] = "1" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["v"] = "2" }, default);
        await r.InsertAsync(collection, new Dictionary<string, object?> { ["v"] = "3" }, default);

        long count = await r.CountAsync(collection, new Dictionary<string, object?>(), default);

        Assert.Equal(3, count);
    }

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task CountAsync_EmptyCollection_ReturnsZero()
    {
        MongoRecordRepository r = repo!;

        long count = await r.CountAsync(collection, new Dictionary<string, object?>(), default);

        Assert.Equal(0, count);
    }
}
