using MaichessDatabaseService.Domain;
using Npgsql;

namespace MaichessDatabaseService.Adapters.Postgres;

internal sealed class PostgresRecordRepository : IRecordRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRecordRepository(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task<DbRecord?> GetAsync(string collection, string id, CancellationToken ct)
    {
        string sql = $"SELECT * FROM {QuoteIdentifier(collection)} WHERE \"id\" = $1";
        await using NpgsqlCommand cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRecord(reader) : null;
    }

    public async Task<IReadOnlyList<DbRecord>> ListAsync(
        string collection,
        IReadOnlyDictionary<string, object?> filter,
        int limit,
        int offset,
        CancellationToken ct)
    {
        var conditions = new List<string>();
        var parameters = new List<object?>();

        foreach ((string key, object? value) in filter)
        {
            parameters.Add(NormalizeValue(value) ?? DBNull.Value);
            conditions.Add($"{QuoteIdentifier(key)} = ${parameters.Count}");
        }

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
        string limitClause = limit > 0 ? $"LIMIT ${parameters.Count + 1}" : string.Empty;
        if (limit > 0)
        {
            parameters.Add(limit);
        }

        string offsetClause = offset > 0 ? $"OFFSET ${parameters.Count + 1}" : string.Empty;
        if (offset > 0)
        {
            parameters.Add(offset);
        }

        string sql = $"SELECT * FROM {QuoteIdentifier(collection)} {where} {limitClause} {offsetClause}";
        await using NpgsqlCommand cmd = _dataSource.CreateCommand(sql);
        foreach (object? p in parameters)
        {
            cmd.Parameters.AddWithValue(p ?? DBNull.Value);
        }

        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<DbRecord>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    public async Task<DbRecord> InsertAsync(
        string collection,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken ct)
    {
        string id = Guid.NewGuid().ToString();
        var allFields = new Dictionary<string, object?>(fields) { ["id"] = id };

        string columns = string.Join(", ", allFields.Keys.Select(QuoteIdentifier));
        string paramNames = string.Join(", ", Enumerable.Range(1, allFields.Count).Select(i => $"${i}"));
        string sql = $"INSERT INTO {QuoteIdentifier(collection)} ({columns}) VALUES ({paramNames}) RETURNING *";

        await using NpgsqlCommand cmd = _dataSource.CreateCommand(sql);
        foreach (object? v in allFields.Values)
        {
            cmd.Parameters.AddWithValue(NormalizeValue(v) ?? DBNull.Value);
        }

        try
        {
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return ReadRecord(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new AlreadyExistsException($"{collection}: unique constraint violated");
        }
    }

    public async Task<DbRecord> UpdateAsync(
        string collection,
        string id,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken ct)
    {
        var setClauses = new List<string>();
        var parameters = new List<object?>();

        foreach ((string key, object? value) in fields)
        {
            parameters.Add(NormalizeValue(value) ?? DBNull.Value);
            setClauses.Add($"{QuoteIdentifier(key)} = ${parameters.Count}");
        }

        parameters.Add(id);
        string sql =
            $"UPDATE {QuoteIdentifier(collection)} SET {string.Join(", ", setClauses)} " +
            $"WHERE \"id\" = ${parameters.Count} RETURNING *";

        await using NpgsqlCommand cmd = _dataSource.CreateCommand(sql);
        foreach (object? p in parameters)
        {
            cmd.Parameters.AddWithValue(p ?? DBNull.Value);
        }

        try
        {
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
            return !await reader.ReadAsync(ct)
                ? throw new NotFoundException($"{collection}/{id} not found")
                : ReadRecord(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new AlreadyExistsException($"{collection}: unique constraint violated");
        }
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct)
    {
        string sql = $"DELETE FROM {QuoteIdentifier(collection)} WHERE \"id\" = $1";
        await using NpgsqlCommand cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        int affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new NotFoundException($"{collection}/{id} not found");
        }
    }

    private static DbRecord ReadRecord(NpgsqlDataReader reader)
    {
        string id = string.Empty;
        var fields = new Dictionary<string, object?>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);

            if (name == "id")
            {
                id = value is Guid g ? g.ToString() : value?.ToString() ?? string.Empty;
            }
            else
            {
                fields[name] = value;
            }
        }

        return new DbRecord(id, fields);
    }

    // Double values coming from google.protobuf.Struct are always float64. Cast integer-valued
    // doubles to long so Npgsql maps them correctly to PostgreSQL integer columns.
    private static object? NormalizeValue(object? value) =>
        value is double d && d == Math.Truncate(d) && !double.IsInfinity(d) ? (long)d : value;

    private static string QuoteIdentifier(string name) =>
        '"' + name.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
