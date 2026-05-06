using MaichessDatabaseService.Adapters;
using MaichessDatabaseService.Adapters.Mongo;
using MaichessDatabaseService.Adapters.Mongo.Migrations;
using MaichessDatabaseService.Adapters.Postgres;
using MaichessDatabaseService.Adapters.Postgres.Migrations;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.Http2.MaxStreamsPerConnection = 200;
});

string adapter = builder.Configuration["Database:Adapter"]
    ?? throw new InvalidOperationException("Database:Adapter is not configured");
string connectionString = builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database:ConnectionString is not configured");
bool readOnly = builder.Configuration.GetValue<bool>("Database:ReadOnly");

#pragma warning disable CA2000 // Singleton lives for the application lifetime — disposal is intentional at process exit
IRecordRepository repo = adapter.ToLowerInvariant() switch
{
    "postgres" => new PostgresRecordRepository(connectionString),
    "mongo" => new MongoRecordRepository(connectionString),
    _ => throw new InvalidOperationException($"Unknown Database:Adapter value: '{adapter}'. Expected 'postgres' or 'mongo'."),
};
#pragma warning restore CA2000

if (readOnly)
{
    repo = new ReadOnlyRecordRepository(repo);
}

builder.Services.AddSingleton(repo);
builder.Services.AddGrpc();

string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://otel-collector:4317";

string serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
    ?? "database-service";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

WebApplication app = builder.Build();

string[] migrationDomains = (builder.Configuration["Database:Migrations"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (migrationDomains.Length > 0)
{
    Dictionary<string, IMigration> available = adapter.ToLowerInvariant() switch
    {
        "postgres" => new Dictionary<string, IMigration>
        {
            ["user"] = new UserPostgresMigration(connectionString),
        },
        "mongo" => new Dictionary<string, IMigration>
        {
            ["match"] = new MatchMongoMigration(connectionString),
        },
        _ => new Dictionary<string, IMigration>(),
    };

    foreach (string domain in migrationDomains)
    {
        if (!available.TryGetValue(domain, out IMigration? migration))
        {
            throw new InvalidOperationException(
                $"No '{adapter}' migration found for domain '{domain}'.");
        }

        await migration.RunAsync(CancellationToken.None);
    }
}

app.MapGrpcService<DatabaseGrpcService>();

await app.RunAsync();
