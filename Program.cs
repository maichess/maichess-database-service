using MaichessDatabaseService.Adapters;
using MaichessDatabaseService.Adapters.Mongo;
using MaichessDatabaseService.Adapters.Postgres;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string adapter = builder.Configuration["Database:Adapter"]
    ?? throw new InvalidOperationException("Database:Adapter is not configured");
string connectionString = builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database:ConnectionString is not configured");
bool readOnly = builder.Configuration.GetValue<bool>("Database:ReadOnly");

IRecordRepository repo = adapter.ToLowerInvariant() switch
{
    "postgres" => new PostgresRecordRepository(connectionString),
    "mongo" => new MongoRecordRepository(connectionString),
    _ => throw new InvalidOperationException($"Unknown Database:Adapter value: '{adapter}'. Expected 'postgres' or 'mongo'."),
};

if (readOnly)
{
    repo = new ReadOnlyRecordRepository(repo);
}

builder.Services.AddSingleton(repo);
builder.Services.AddGrpc();

WebApplication app = builder.Build();

app.MapGrpcService<DatabaseGrpcService>();

app.Run();
