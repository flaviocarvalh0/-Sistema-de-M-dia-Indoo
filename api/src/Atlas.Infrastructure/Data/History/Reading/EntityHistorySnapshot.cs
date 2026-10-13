namespace Atlas.Infrastructure.Data.History.Reading;

/// <summary>
/// Representa o estado completo de um registro em determinado momento.
/// </summary>
internal sealed record EntityHistorySnapshot
{
    public required long HistoryId { get; init; }

    public required long RecordId { get; init; }

    public required DateTimeOffset HistoryCreatedAt { get; init; }

    public required IReadOnlyDictionary<string, object?> Values { get; init; }
}
