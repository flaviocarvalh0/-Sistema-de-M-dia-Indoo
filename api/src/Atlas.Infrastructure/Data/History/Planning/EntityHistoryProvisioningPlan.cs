using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Planning;
/// <summary>
/// Plano declarativo das operações necessárias para provisionar ou
/// sincronizar o histórico de uma entidade.
/// </summary>
internal sealed record EntityHistoryProvisioningPlan
{
    public required EntityHistoryDefinition Definition { get; init; }

    public required IReadOnlyCollection<EntityHistoryProvisioningStep> Steps { get; init; }

    public bool HasChanges => Steps.Count > 0;
}
