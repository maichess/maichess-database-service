namespace MaichessDatabaseService.Domain;

internal sealed class AlreadyExistsException(string message) : Exception(message);
