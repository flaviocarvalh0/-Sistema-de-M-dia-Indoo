using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Planning;
/// <summary>
/// Representa uma operação individual do plano de provisionamento histórico.
/// </summary>
internal sealed record EntityHistoryProvisioningStep
{
    public required EntityHistoryProvisioningAction Action { get; init; }

    /// <summary>
    /// Coluna relacionada à operação, quando a ação for AddColumn.
    /// </summary>
    public EntityHistoryColumnDefinition? Column { get; init; }

    /// <summary>
    /// Texto explicativo para diagnóstico e logs.
    /// </summary>
    public required string Description { get; init; }
}
