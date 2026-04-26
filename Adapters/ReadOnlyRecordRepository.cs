using MaichessDatabaseService.Domain;

namespace MaichessDatabaseService.Adapters;

internal sealed class ReadOnlyRecordRepository(IRecordRepository inner) : IRecordRepository
{
    public Task<DbRecord?> GetAsync(string collection, string id, CancellationToken ct) =>
        inner.GetAsync(collection, id, ct);

    public Task<IReadOnlyList<DbRecord>> ListAsync(
        string collection,
        IReadOnlyDictionary<string, object?> filter,
        int limit,
        int offset,
        CancellationToken ct) =>
        inner.ListAsync(collection, filter, limit, offset, ct);

    public Task<DbRecord> InsertAsync(string collection, IReadOnlyDictionary<string, object?> fields, CancellationToken ct) =>
        throw new ReadOnlyViolationException();

    public Task<DbRecord> UpdateAsync(string collection, string id, IReadOnlyDictionary<string, object?> fields, CancellationToken ct) =>
        throw new ReadOnlyViolationException();

    public Task DeleteAsync(string collection, string id, CancellationToken ct) =>
        throw new ReadOnlyViolationException();
}
