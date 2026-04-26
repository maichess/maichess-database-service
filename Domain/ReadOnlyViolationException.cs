namespace MaichessDatabaseService.Domain;

internal sealed class ReadOnlyViolationException()
    : Exception("This instance is configured as read-only.");
