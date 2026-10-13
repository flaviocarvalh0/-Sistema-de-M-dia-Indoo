namespace Atlas.Infrastructure.Data.History.Inspection;

/// <summary>
/// Representa o estado atual da infraestrutura histórica de uma entidade
/// existente no PostgreSQL.
/// </summary>
internal sealed record EntityHistoryInfrastructureState
{
    public required bool SchemaExists { get; init; }

    public required bool TableExists { get; init; }

    public required bool FunctionExists { get; init; }

    public required bool TriggerExists { get; init; }

    public required bool IndexExists { get; init; }

    public required bool ForeignKeyExists { get; init; }

    public required IReadOnlyCollection<EntityHistoryDatabaseColumn> Columns { get; init; }
}
