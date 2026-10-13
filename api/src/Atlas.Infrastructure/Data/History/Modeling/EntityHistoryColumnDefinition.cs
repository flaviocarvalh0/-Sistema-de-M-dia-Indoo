using Microsoft.EntityFrameworkCore.Metadata;

namespace Atlas.Infrastructure.Data.History.Modeling;

/// <summary>
/// Representa uma coluna física que será replicada na tabela histórica.
/// </summary>
internal sealed record EntityHistoryColumnDefinition
{
    public required string PropertyName { get; init; }

    public required string ColumnName { get; init; }

    public required string StoreType { get; init; }

    public required bool IsNullable { get; init; }

    public required bool IsPrimaryKey { get; init; }

    public required bool IsShadowProperty { get; init; }

    public required ValueGenerated ValueGenerated { get; init; }
}
