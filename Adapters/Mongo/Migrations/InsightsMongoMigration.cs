using System.Diagnostics.CodeAnalysis;
using MaichessDatabaseService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MaichessDatabaseService.Adapters.Mongo.Migrations;

// insights-db schema. The .NET insights control plane (tasks 05/06) reads these via
// the generic Database gRPC CRUD contract; the Scala Spark module writes the
// materialized metric collections via the Spark Mongo connector (the one documented
// write-path exception). The control plane owns insights_jobs + insights_corpora (the
// catalog it creates on submit and updates as SparkApplications advance).
[ExcludeFromCodeCoverage]
internal sealed class InsightsMongoMigration : IMigration
{
    private readonly string connectionString;

    public InsightsMongoMigration(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public string Domain => "insights";

    public async Task RunAsync(CancellationToken ct)
    {
        using MongoClient client = new(connectionString);

        // All DatabaseService Mongo instances share the "maichess" database, separated
        // by collection name (the adapter hardcodes it); the insights_* collections live
        // here so the control plane reads them via the same gRPC CRUD path. The Spark
        // module is pointed at the same database (--mongo-db maichess) by the control
        // plane so its connector writes land where the API reads.
        IMongoDatabase db = client.GetDatabase("maichess");

        string[] collections =
        [
            "insights_jobs",
            "insights_corpora",
            "insights_openings",
            "insights_endgames",
            "insights_positions",
            "insights_tricky",
            "insights_summary",
        ];
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

        // Jobs are listed newest-first and filtered by status in the control plane.
        IMongoCollection<BsonDocument> jobs = db.GetCollection<BsonDocument>("insights_jobs");
        await jobs.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status")),
            cancellationToken: ct);
        await jobs.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Descending("created_at_ms")),
            cancellationToken: ct);

        // Corpora are listed newest-first.
        IMongoCollection<BsonDocument> corpora = db.GetCollection<BsonDocument>("insights_corpora");
        await corpora.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Descending("created_at_ms")),
            cancellationToken: ct);

        // Every metric row carries corpus_id so a collection can hold multiple corpora;
        // the query API (task 06) filters each read by it.
        string[] metricCollections =
            ["insights_openings", "insights_endgames", "insights_positions", "insights_tricky", "insights_summary"];
        foreach (string name in metricCollections)
        {
            IMongoCollection<BsonDocument> metric = db.GetCollection<BsonDocument>(name);
            await metric.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("corpus_id")),
                cancellationToken: ct);
        }
    }
}
