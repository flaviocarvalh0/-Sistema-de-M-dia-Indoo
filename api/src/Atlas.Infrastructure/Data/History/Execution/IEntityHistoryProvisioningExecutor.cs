using Atlas.Infrastructure.Data.History.Planning;

namespace Atlas.Infrastructure.Data.History.Execution;

internal interface IEntityHistoryProvisioningExecutor
{
    Task ExecuteAsync(
        EntityHistoryProvisioningPlan plan,
        CancellationToken cancellationToken = default);
}
