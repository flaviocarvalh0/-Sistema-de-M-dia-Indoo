using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Data.History.Modeling;
internal interface IEntityHistoryModelResolver
{
    EntityHistoryDefinition Resolve(EntityMetadata metadata);
}
