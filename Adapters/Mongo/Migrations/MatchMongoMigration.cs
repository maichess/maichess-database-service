using System.Diagnostics.CodeAnalysis;
using MaichessDatabaseService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MaichessDatabaseService.Adapters.Mongo.Migrations;

[ExcludeFromCodeCoverage]
internal sealed class MatchMongoMigration : IMigration
{
    private readonly string connectionString;

    public MatchMongoMigration(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public string Domain => "match";

    public async Task RunAsync(CancellationToken ct)
    {
        using MongoClient client = new(connectionString);
        IMongoDatabase db = client.GetDatabase("maichess");

        try
        {
            await db.CreateCollectionAsync("matches", cancellationToken: ct);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "NamespaceExists")
        {
            // Collection already exists — no-op
        }

        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("matches");
        IndexKeysDefinition<BsonDocument> keys = Builders<BsonDocument>.IndexKeys.Ascending("status");
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys), cancellationToken: ct);
    }
}
