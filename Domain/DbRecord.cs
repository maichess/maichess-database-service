namespace MaichessDatabaseService.Domain;

internal sealed record DbRecord(string Id, IReadOnlyDictionary<string, object?> Fields);
