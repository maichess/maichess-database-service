using System.Diagnostics.CodeAnalysis;
using MaichessDatabaseService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MaichessDatabaseService.Adapters.Mongo.Migrations;

[ExcludeFromCodeCoverage]
internal sealed class AnticheatMongoMigration : IMigration
{
    private readonly string connectionString;

    public AnticheatMongoMigration(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public string Domain => "anticheat";

    public async Task RunAsync(CancellationToken ct)
    {
        using MongoClient client = new(connectionString);
        IMongoDatabase db = client.GetDatabase("maichess");

        string[] collections = ["cases", "audit"];
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

        // One open case per user — looked up by user_id on every verdict.
        IMongoCollection<BsonDocument> cases = db.GetCollection<BsonDocument>("cases");
        await cases.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("user_id")),
            cancellationToken: ct);
        await cases.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status")),
            cancellationToken: ct);

        // Audit rows are listed per case for the Dev evidence view.
        IMongoCollection<BsonDocument> audit = db.GetCollection<BsonDocument>("audit");
        await audit.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("case_id")),
            cancellationToken: ct);
    }
}
