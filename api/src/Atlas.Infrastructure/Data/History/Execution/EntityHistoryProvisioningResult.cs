using Atlas.Infrastructure.Data.History.Planning;

namespace Atlas.Infrastructure.Data.History.Execution;
internal sealed record EntityHistoryProvisioningResult
{
    public required string SourceSchema { get; init; }

    public required string SourceTable { get; init; }

    public required string HistorySchema { get; init; }

    public required string HistoryTable { get; init; }

    public required IReadOnlyCollection<
        EntityHistoryProvisioningAction> ExecutedActions
    { get; init; }
}
