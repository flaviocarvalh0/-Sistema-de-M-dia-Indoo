namespace Atlas.Infrastructure.Data.History.Reading;


/// <summary>
/// Resultado de uma leitura paginada de snapshots históricos.
/// </summary>
internal sealed record EntityHistorySnapshotBatch
{
    /// <summary>
    /// Snapshots ordenados do mais recente para o mais antigo.
    /// </summary>
    public required IReadOnlyCollection<EntityHistorySnapshot> Snapshots { get; init; }

    /// <summary>
    /// Snapshot imediatamente anterior ao último item da página.
    /// É utilizado para comparar a última versão retornada com sua predecessora.
    /// </summary>
    public EntityHistorySnapshot? ComparisonBaseline { get; init; }

    public required bool HasMore { get; init; }

    public long? NextCursor { get; init; }
}
