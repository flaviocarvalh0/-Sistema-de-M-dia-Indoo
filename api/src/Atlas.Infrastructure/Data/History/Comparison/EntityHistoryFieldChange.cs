namespace Atlas.Infrastructure.Data.History.Comparison;

/// <summary>
/// Representa a alteração de um campo entre duas versões consecutivas.
/// </summary>
internal sealed record EntityHistoryFieldChange
{
    /// <summary>
    /// Nome físico da coluna histórica.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Nome da propriedade CLR associada à coluna.
    /// </summary>
    public required string PropertyName { get; init; }

    public object? PreviousValue { get; init; }

    public object? CurrentValue { get; init; }
}
