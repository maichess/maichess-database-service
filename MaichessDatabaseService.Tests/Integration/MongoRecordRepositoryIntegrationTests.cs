using MaichessDatabaseService.Adapters.Mongo;
using MaichessDatabaseService.Domain;
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

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task InsertAsync_SuppliedId_UsesIdAndIsRetrievable()
    {
        MongoRecordRepository r = repo!;
        string id = Guid.NewGuid().ToString();

        DbRecord inserted = await r.InsertAsync(
            collection, new Dictionary<string, object?> { ["id"] = id, ["color"] = "red" }, default);

        Assert.Equal(id, inserted.Id);
        Assert.False(inserted.Fields.ContainsKey("id"));

        DbRecord? fetched = await r.GetAsync(collection, id, default);
        Assert.NotNull(fetched);
        Assert.Equal("red", fetched!.Fields["color"]);
    }

    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task InsertAsync_DuplicateSuppliedId_ThrowsAlreadyExists()
    {
        MongoRecordRepository r = repo!;
        string id = Guid.NewGuid().ToString();

        await r.InsertAsync(collection, new Dictionary<string, object?> { ["id"] = id }, default);

        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            r.InsertAsync(collection, new Dictionary<string, object?> { ["id"] = id }, default));
    }

    // A document written directly by another writer (e.g. the insights Spark connector)
    // has a server-generated ObjectId _id, not the string _id this service assigns. List
    // must surface it (rendering the id as its string form) rather than throwing on the
    // whole collection.
    [RequiresEnvVarFact("MONGO_CONNECTION_STRING")]
    public async Task ListAsync_DocumentWithObjectIdId_DoesNotThrowAndStringifiesId()
    {
        MongoRecordRepository r = repo!;
        string cs = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")!;
        var oid = MongoDB.Bson.ObjectId.GenerateNewId();
        await new MongoDB.Driver.MongoClient(cs)
            .GetDatabase("maichess")
            .GetCollection<MongoDB.Bson.BsonDocument>(collection)
            .InsertOneAsync(new MongoDB.Bson.BsonDocument { ["_id"] = oid, ["corpusId"] = "c1" });

        var records = await r.ListAsync(collection, new Dictionary<string, object?>(), 0, 0, default);

        Assert.Single(records);
        Assert.Equal(oid.ToString(), records[0].Id);
        Assert.Equal("c1", records[0].Fields["corpusId"]);
    }
}
