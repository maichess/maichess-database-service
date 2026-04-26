namespace MaichessDatabaseService.Domain;

internal sealed class NotFoundException(string message) : Exception(message);
