using Atlas.Infrastructure.Data.History.Inspection;
using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Planning;
internal interface IEntityHistorySchemaComparer
{
    EntityHistoryProvisioningPlan Compare(
        EntityHistoryDefinition definition,
        EntityHistoryInfrastructureState state);
}
