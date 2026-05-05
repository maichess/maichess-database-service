namespace MaichessDatabaseService.Domain;

internal interface IMigration
{
    string Domain { get; }

    Task RunAsync(CancellationToken ct);
}
