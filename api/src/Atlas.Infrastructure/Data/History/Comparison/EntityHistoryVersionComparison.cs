namespace Atlas.Infrastructure.Data.History.Comparison;

/// <summary>
/// Resultado da comparação de um snapshot com sua versão anterior.
/// </summary>
internal sealed record EntityHistoryVersionComparison
{
    public required long HistoryId { get; init; }

    public required long RecordId { get; init; }

    public required DateTimeOffset HistoryCreatedAt { get; init; }

    /// <summary>
    /// Indica que esta é a primeira versão histórica conhecida
    /// para o registro no conjunto consultado.
    /// </summary>
    public required bool IsInitialVersion { get; init; }

    public required IReadOnlyCollection<EntityHistoryFieldChange> Changes { get; init; }

    /// <summary>
    /// Valores completos da versão. Mantidos para exibição detalhada
    /// e futuras operações de comparação entre versões arbitrárias.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Values { get; init; }
}
