using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Inspection;
internal interface IEntityHistorySchemaInspector
{
    Task<EntityHistoryInfrastructureState> InspectAsync(
        EntityHistoryDefinition definition,
        CancellationToken cancellationToken = default);
}
