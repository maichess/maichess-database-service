namespace MaichessDatabaseService.Domain;

// A database operation failed for a reason the service does not model as a
// specific outcome (e.g. a schema mismatch or connectivity error). Adapters
// translate driver-specific exceptions into this so DB types never escape the
// adapter; the gRPC layer surfaces it as StatusCode.Internal.
internal sealed class RepositoryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
