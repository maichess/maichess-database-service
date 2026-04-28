namespace MaichessDatabaseService.Domain;

internal interface IRecordRepository
{
    Task<DbRecord?> GetAsync(string collection, string id, CancellationToken ct);

    Task<IReadOnlyList<DbRecord>> ListAsync(
        string collection,
        IReadOnlyDictionary<string, object?> filter,
        int limit,
        int offset,
        CancellationToken ct);

    Task<DbRecord> InsertAsync(string collection, IReadOnlyDictionary<string, object?> fields, CancellationToken ct);

    Task<DbRecord> UpdateAsync(string collection, string id, IReadOnlyDictionary<string, object?> fields, CancellationToken ct);

    Task DeleteAsync(string collection, string id, CancellationToken ct);

    Task<long> DeleteWhereAsync(string collection, IReadOnlyDictionary<string, object?> filter, CancellationToken ct);

    Task<long> CountAsync(string collection, IReadOnlyDictionary<string, object?> filter, CancellationToken ct);
}
