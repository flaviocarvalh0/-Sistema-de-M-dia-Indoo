using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Reading;
internal interface IEntityHistorySnapshotReader
{
    Task<EntityHistorySnapshotBatch> ReadAsync(
        EntityHistoryDefinition definition,
        long recordId,
        long? beforeHistoryId = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default);
}
