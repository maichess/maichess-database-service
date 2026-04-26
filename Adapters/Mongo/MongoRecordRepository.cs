using MaichessDatabaseService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MaichessDatabaseService.Adapters.Mongo;

internal sealed class MongoRecordRepository : IRecordRepository
{
    private readonly IMongoDatabase db;

    public MongoRecordRepository(string connectionString)
    {
        var client = new MongoClient(connectionString);
        db = client.GetDatabase("maichess");
    }

    public async Task<DbRecord?> GetAsync(string collection, string id, CancellationToken ct)
    {
        BsonDocument? doc = await GetCollection(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToRecord(doc);
    }

    public async Task<IReadOnlyList<DbRecord>> ListAsync(
        string collection,
        IReadOnlyDictionary<string, object?> filter,
        int limit,
        int offset,
        CancellationToken ct)
    {
        IFindFluent<BsonDocument, BsonDocument> query = GetCollection(collection)
            .Find(BuildFilter(filter));

        if (offset > 0)
        {
            query = query.Skip(offset);
        }

        if (limit > 0)
        {
            query = query.Limit(limit);
        }

        List<BsonDocument> docs = await query.ToListAsync(ct);
        return docs.Select(ToRecord).ToList();
    }

    public async Task<DbRecord> InsertAsync(
        string collection,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken ct)
    {
        string id = Guid.NewGuid().ToString();
        BsonDocument doc = FieldsToBson(fields);
        doc["_id"] = id;
        await GetCollection(collection).InsertOneAsync(doc, cancellationToken: ct);
        return ToRecord(doc);
    }

    public async Task<DbRecord> UpdateAsync(
        string collection,
        string id,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken ct)
    {
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update.Combine(
            fields.Select(kv => Builders<BsonDocument>.Update.Set(kv.Key, BsonValue.Create(kv.Value))));

        BsonDocument? result = await GetCollection(collection)
            .FindOneAndUpdateAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                update,
                new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                ct);

        return result is null
            ? throw new NotFoundException($"{collection}/{id} not found")
            : ToRecord(result);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct)
    {
        DeleteResult result = await GetCollection(collection)
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), ct);

        if (result.DeletedCount == 0)
        {
            throw new NotFoundException($"{collection}/{id} not found");
        }
    }

    private IMongoCollection<BsonDocument> GetCollection(string name) =>
        db.GetCollection<BsonDocument>(name);

    private static FilterDefinition<BsonDocument> BuildFilter(IReadOnlyDictionary<string, object?> filter)
    {
        if (filter.Count == 0)
        {
            return Builders<BsonDocument>.Filter.Empty;
        }

        IEnumerable<FilterDefinition<BsonDocument>> conditions = filter.Select(kv =>
        {
            string field = kv.Key == "id" ? "_id" : kv.Key;
            return Builders<BsonDocument>.Filter.Eq(field, BsonValue.Create(kv.Value));
        });

        return Builders<BsonDocument>.Filter.And(conditions);
    }

    private static BsonDocument FieldsToBson(IReadOnlyDictionary<string, object?> fields)
    {
        var doc = new BsonDocument();
        foreach ((string key, object? value) in fields)
        {
            doc[key] = BsonValue.Create(value);
        }

        return doc;
    }

    private static DbRecord ToRecord(BsonDocument doc)
    {
        string id = doc.TryGetValue("_id", out BsonValue idVal) ? idVal.AsString : string.Empty;
        var fields = new Dictionary<string, object?>();

        foreach (BsonElement el in doc)
        {
            if (el.Name == "_id")
            {
                continue;
            }

            fields[el.Name] = BsonTypeMapper.MapToDotNetValue(el.Value);
        }

        return new DbRecord(id, fields);
    }
}
