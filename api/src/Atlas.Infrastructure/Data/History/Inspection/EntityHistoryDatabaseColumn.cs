namespace Atlas.Infrastructure.Data.History.Inspection;

/// <summary>
/// Representa uma coluna já existente na tabela histórica do PostgreSQL.
/// </summary>
internal sealed record EntityHistoryDatabaseColumn
{
    public required string Name { get; init; }

    public required string StoreType { get; init; }

    public required bool IsNullable { get; init; }
}
