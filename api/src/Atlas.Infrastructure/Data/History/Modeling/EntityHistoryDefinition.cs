namespace Atlas.Infrastructure.Data.History.Modeling;

/// <summary>
/// Representa a definição física necessária para provisionar
/// o histórico de uma entidade mapeada pelo Entity Framework Core.
/// </summary>
internal sealed record EntityHistoryDefinition
{
    public required Type ClrType { get; init; }

    public required string SourceSchema { get; init; }

    public required string SourceTable { get; init; }

    public required string HistorySchema { get; init; }

    public required string HistoryTable { get; init; }

    public required string PrimaryKeyColumn { get; init; }

    public required string PrimaryKeyStoreType { get; init; }

    public required IReadOnlyCollection<EntityHistoryColumnDefinition> Columns { get; init; }
}
