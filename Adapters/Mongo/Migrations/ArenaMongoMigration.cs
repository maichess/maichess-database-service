using System.Diagnostics.CodeAnalysis;
using MaichessDatabaseService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MaichessDatabaseService.Adapters.Mongo.Migrations;

[ExcludeFromCodeCoverage]
internal sealed class ArenaMongoMigration : IMigration
{
    private readonly string connectionString;

    public ArenaMongoMigration(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public string Domain => "arena";

    public async Task RunAsync(CancellationToken ct)
    {
        using MongoClient client = new(connectionString);
        IMongoDatabase db = client.GetDatabase("maichess");

        string[] collections = ["collections", "games", "settings"];
        foreach (string name in collections)
        {
            try
            {
                await db.CreateCollectionAsync(name, cancellationToken: ct);
            }
            catch (MongoCommandException ex) when (ex.CodeName == "NamespaceExists")
            {
                // Collection already exists — no-op
            }
        }

        IMongoCollection<BsonDocument> games = db.GetCollection<BsonDocument>("games");
        await games.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("collection_id")),
            cancellationToken: ct);
        await games.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status")),
            cancellationToken: ct);

        IMongoCollection<BsonDocument> setups = db.GetCollection<BsonDocument>("collections");
        await setups.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status")),
            cancellationToken: ct);

        IMongoCollection<BsonDocument> settings = db.GetCollection<BsonDocument>("settings");
        await settings.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("key")),
            cancellationToken: ct);
    }
}
