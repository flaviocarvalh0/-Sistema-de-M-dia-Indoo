using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Reading;

namespace Atlas.Infrastructure.Data.History.Comparison;
internal interface IEntityHistorySnapshotComparer
{
    IReadOnlyCollection<EntityHistoryVersionComparison> Compare(
        EntityHistoryDefinition definition,
        EntityHistorySnapshotBatch batch);
}
