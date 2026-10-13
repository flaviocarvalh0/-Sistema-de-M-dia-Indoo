using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Atlas.Infrastructure.Data.History.Modeling;
/// <summary>
/// Resolve os metadados físicos de uma entidade mapeada pelo EF Core
/// a partir do registro correspondente em EntityMetadata.
/// </summary>
internal sealed class EntityHistoryModelResolver : IEntityHistoryModelResolver
{
    private const string HistorySchema = "history";

    private readonly ApplicationDbContext _context;

    public EntityHistoryModelResolver(ApplicationDbContext context)
    {
        _context = context;
    }

    public EntityHistoryDefinition Resolve(EntityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(metadata.TableName))
        {
            throw new InvalidOperationException(
                "O metadado da entidade não possui um nome físico de tabela.");
        }

        var matches = _context.Model
            .GetEntityTypes()
            .Where(entityType =>
                string.Equals(
                    entityType.GetTableName(),
                    metadata.TableName,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nenhuma entidade do EF Core foi encontrada para a tabela " +
                $"'{metadata.TableName}'.");
        }

        if (matches.Count > 1)
        {
            var mappedEntities = string.Join(
                ", ",
                matches.Select(x => x.ClrType.FullName));

            throw new InvalidOperationException(
                $"Mais de uma entidade foi mapeada para a tabela " +
                $"'{metadata.TableName}': {mappedEntities}.");
        }

        var entityType = matches.Single();

        ValidateEntityEligibility(entityType, metadata);

        var sourceTable = entityType.GetTableName()
            ?? throw new InvalidOperationException(
                $"A entidade '{entityType.ClrType.Name}' não possui tabela mapeada.");

        var sourceSchema = entityType.GetSchema() ?? "public";

        var storeObject = StoreObjectIdentifier.Table(
            sourceTable,
            entityType.GetSchema());

        var primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(
                $"A entidade '{entityType.ClrType.Name}' não possui chave primária.");

        if (primaryKey.Properties.Count != 1)
        {
            throw new NotSupportedException(
                $"A entidade '{entityType.ClrType.Name}' possui chave composta. " +
                "O engenho de histórico suporta inicialmente apenas chaves simples.");
        }

        var primaryKeyProperty = primaryKey.Properties.Single();

        var primaryKeyColumn =
            primaryKeyProperty.GetColumnName(storeObject)
            ?? throw new InvalidOperationException(
                $"Não foi possível resolver a coluna da chave primária da entidade " +
                $"'{entityType.ClrType.Name}'.");

        var primaryKeyStoreType =
            primaryKeyProperty.GetColumnType()
            ?? primaryKeyProperty.GetRelationalTypeMapping().StoreType;

        var columns = entityType
            .GetProperties()
            .Select(property => MapColumn(property, storeObject, primaryKey))
            .Where(column => column is not null)
            .Cast<EntityHistoryColumnDefinition>()
            .ToList();

        return new EntityHistoryDefinition
        {
            ClrType = entityType.ClrType,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            HistorySchema = HistorySchema,
            HistoryTable = BuildHistoryTableName(sourceSchema, sourceTable),
            PrimaryKeyColumn = primaryKeyColumn,
            PrimaryKeyStoreType = primaryKeyStoreType,
            Columns = columns
        };
    }

    private static EntityHistoryColumnDefinition? MapColumn(
        IProperty property,
        StoreObjectIdentifier storeObject,
        IKey primaryKey)
    {
        var columnName = property.GetColumnName(storeObject);

        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        // A shadow property "Version", associada ao xmin,
        // não será persistida no histórico nesta versão.
        if (property.IsShadowProperty() &&
            string.Equals(
                columnName,
                "xmin",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var storeType =
            property.GetColumnType()
            ?? property.GetRelationalTypeMapping().StoreType;

        return new EntityHistoryColumnDefinition
        {
            PropertyName = property.Name,
            ColumnName = columnName,
            StoreType = storeType,
            IsNullable = property.IsColumnNullable(storeObject),
            IsPrimaryKey = primaryKey.Properties.Contains(property),
            IsShadowProperty = property.IsShadowProperty(),
            ValueGenerated = property.ValueGenerated
        };
    }

    private static void ValidateEntityEligibility(
        IEntityType entityType,
        EntityMetadata metadata)
    {
        if (entityType.IsOwned())
        {
            throw new NotSupportedException(
                $"A entidade owned '{entityType.ClrType.Name}' não pode possuir " +
                "histórico independente.");
        }

        if (entityType.GetViewName() is not null)
        {
            throw new NotSupportedException(
                $"A entidade '{entityType.ClrType.Name}' está mapeada para uma view.");
        }

        var schemaName = entityType.GetSchema() ?? "public";

        if (string.Equals(
                schemaName,
                "history",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"A entidade '{entityType.ClrType.Name}' pertence ao schema " +
                "reservado de histórico.");
        }

        // EntityMetadata será consultada pelas funções de trigger para verificar
        // se a captura está habilitada. Versioná-la nesta etapa criaria uma
        // dependência circular desnecessária.
        if (entityType.ClrType == typeof(EntityMetadata))
        {
            throw new NotSupportedException(
                "A entidade EntityMetadata não pode possuir histórico nesta versão.");
        }
    }

    private static string BuildHistoryTableName(
        string sourceSchema,
        string sourceTable)
    {
        return $"{sourceSchema}_{sourceTable}_history".ToLowerInvariant();
    }
}
