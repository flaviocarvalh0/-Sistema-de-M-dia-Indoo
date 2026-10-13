using System.Text.RegularExpressions;
using Atlas.Infrastructure.Data.History.Inspection;
using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Planning;
/// <summary>
/// Compara a definição histórica produzida pelo modelo do EF Core com
/// a infraestrutura encontrada no PostgreSQL e produz um plano aditivo.
/// </summary>
internal sealed class EntityHistorySchemaComparer
    : IEntityHistorySchemaComparer
{
    public EntityHistoryProvisioningPlan Compare(
        EntityHistoryDefinition definition,
        EntityHistoryInfrastructureState state)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(state);

        var steps = new List<EntityHistoryProvisioningStep>();

        if (!state.SchemaExists)
        {
            steps.Add(new EntityHistoryProvisioningStep
            {
                Action = EntityHistoryProvisioningAction.CreateSchema,
                Description =
                    $"Criar o schema histórico '{definition.HistorySchema}'."
            });
        }

        if (!state.TableExists)
        {
            AddCompleteInfrastructureSteps(
                definition,
                steps);

            return BuildPlan(definition, steps);
        }

        CompareExistingTable(
            definition,
            state,
            steps);

        // A função deve ser sempre recriada quando a tabela já existe.
        // Isso garante que novas colunas e mudanças no corpo da função
        // sejam refletidas sem precisar comparar o código PL/pgSQL.
        steps.Add(new EntityHistoryProvisioningStep
        {
            Action =
                EntityHistoryProvisioningAction.CreateOrReplaceFunction,
            Description =
                $"Criar ou atualizar a função de captura da tabela " +
                $"'{definition.SourceSchema}.{definition.SourceTable}'."
        });

        if (!state.TriggerExists)
        {
            steps.Add(new EntityHistoryProvisioningStep
            {
                Action = EntityHistoryProvisioningAction.CreateTrigger,
                Description =
                    $"Criar a trigger histórica da tabela " +
                    $"'{definition.SourceSchema}.{definition.SourceTable}'."
            });
        }

        if (!state.IndexExists)
        {
            steps.Add(new EntityHistoryProvisioningStep
            {
                Action = EntityHistoryProvisioningAction.CreateIndex,
                Description =
                    $"Criar o índice de consulta da tabela histórica " +
                    $"'{definition.HistorySchema}.{definition.HistoryTable}'."
            });
        }

        if (!state.ForeignKeyExists)
        {
            steps.Add(new EntityHistoryProvisioningStep
            {
                Action = EntityHistoryProvisioningAction.CreateForeignKey,
                Description =
                    $"Criar a chave estrangeira entre " +
                    $"'{definition.HistorySchema}.{definition.HistoryTable}' e " +
                    $"'{definition.SourceSchema}.{definition.SourceTable}'."
            });
        }

        return BuildPlan(definition, steps);
    }

    private static void AddCompleteInfrastructureSteps(
        EntityHistoryDefinition definition,
        ICollection<EntityHistoryProvisioningStep> steps)
    {
        steps.Add(new EntityHistoryProvisioningStep
        {
            Action = EntityHistoryProvisioningAction.CreateTable,
            Description =
                $"Criar a tabela histórica " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}'."
        });

        // O CREATE TABLE inicial já poderá incluir a FK.
        // Portanto, não adicionamos CreateForeignKey separadamente aqui.

        steps.Add(new EntityHistoryProvisioningStep
        {
            Action = EntityHistoryProvisioningAction.CreateIndex,
            Description =
                $"Criar o índice de consulta da tabela histórica " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}'."
        });

        steps.Add(new EntityHistoryProvisioningStep
        {
            Action =
                EntityHistoryProvisioningAction.CreateOrReplaceFunction,
            Description =
                $"Criar a função de captura da tabela " +
                $"'{definition.SourceSchema}.{definition.SourceTable}'."
        });

        steps.Add(new EntityHistoryProvisioningStep
        {
            Action = EntityHistoryProvisioningAction.CreateTrigger,
            Description =
                $"Criar a trigger histórica da tabela " +
                $"'{definition.SourceSchema}.{definition.SourceTable}'."
        });
    }

    private static void CompareExistingTable(
        EntityHistoryDefinition definition,
        EntityHistoryInfrastructureState state,
        ICollection<EntityHistoryProvisioningStep> steps)
    {
        var existingColumns = state.Columns
            .ToDictionary(
                column => column.Name,
                StringComparer.OrdinalIgnoreCase);

        ValidateTechnicalColumns(
            definition,
            existingColumns);

        foreach (var expectedColumn in definition.Columns
                     .Where(column => !column.IsPrimaryKey))
        {
            if (!existingColumns.TryGetValue(
                    expectedColumn.ColumnName,
                    out var existingColumn))
            {
                steps.Add(new EntityHistoryProvisioningStep
                {
                    Action = EntityHistoryProvisioningAction.AddColumn,
                    Column = expectedColumn,
                    Description =
                        $"Adicionar a coluna '{expectedColumn.ColumnName}' à " +
                        $"tabela histórica " +
                        $"'{definition.HistorySchema}.{definition.HistoryTable}'."
                });

                continue;
            }

            ValidateColumnCompatibility(
                definition,
                expectedColumn,
                existingColumn);
        }
    }

    private static void ValidateTechnicalColumns(
        EntityHistoryDefinition definition,
        IReadOnlyDictionary<string, EntityHistoryDatabaseColumn> existingColumns)
    {
        ValidateRequiredTechnicalColumn(
            definition,
            existingColumns,
            "history_id",
            "bigint",
            isNullable: false);

        ValidateRequiredTechnicalColumn(
            definition,
            existingColumns,
            "record_id",
            definition.PrimaryKeyStoreType,
            isNullable: false);

        ValidateRequiredTechnicalColumn(
            definition,
            existingColumns,
            "history_created_at",
            "timestamp with time zone",
            isNullable: false);
    }

    private static void ValidateRequiredTechnicalColumn(
        EntityHistoryDefinition definition,
        IReadOnlyDictionary<string, EntityHistoryDatabaseColumn> existingColumns,
        string columnName,
        string expectedStoreType,
        bool isNullable)
    {
        if (!existingColumns.TryGetValue(
                columnName,
                out var existingColumn))
        {
            throw new EntityHistorySchemaIncompatibleException(
                $"A tabela histórica " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}' já existe, " +
                $"mas não possui a coluna técnica obrigatória '{columnName}'.");
        }

        if (!AreStoreTypesCompatible(
                expectedStoreType,
                existingColumn.StoreType))
        {
            throw new EntityHistorySchemaIncompatibleException(
                $"A coluna técnica '{columnName}' da tabela histórica " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}' possui " +
                $"o tipo '{existingColumn.StoreType}', mas o tipo esperado é " +
                $"'{expectedStoreType}'.");
        }

        if (existingColumn.IsNullable != isNullable)
        {
            throw new EntityHistorySchemaIncompatibleException(
                $"A coluna técnica '{columnName}' da tabela histórica " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}' possui " +
                $"nulabilidade incompatível.");
        }
    }

    private static void ValidateColumnCompatibility(
        EntityHistoryDefinition definition,
        EntityHistoryColumnDefinition expectedColumn,
        EntityHistoryDatabaseColumn existingColumn)
    {
        if (!AreStoreTypesCompatible(
                expectedColumn.StoreType,
                existingColumn.StoreType))
        {
            throw new EntityHistorySchemaIncompatibleException(
                $"A coluna histórica '{expectedColumn.ColumnName}' da tabela " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}' possui " +
                $"o tipo '{existingColumn.StoreType}', enquanto a entidade atual " +
                $"exige '{expectedColumn.StoreType}'. A conversão deve ser tratada " +
                $"por uma migration ou intervenção explícita.");
        }

        // Colunas copiadas da entidade devem aceitar NULL na tabela histórica,
        // independentemente da nulabilidade atual da origem.
        if (!existingColumn.IsNullable)
        {
            throw new EntityHistorySchemaIncompatibleException(
                $"A coluna histórica '{expectedColumn.ColumnName}' da tabela " +
                $"'{definition.HistorySchema}.{definition.HistoryTable}' deveria " +
                "aceitar NULL para preservar compatibilidade com versões antigas.");
        }
    }

    private static bool AreStoreTypesCompatible(
        string expected,
        string actual)
    {
        var normalizedExpected = NormalizeStoreType(expected);
        var normalizedActual = NormalizeStoreType(actual);

        return string.Equals(
            normalizedExpected,
            normalizedActual,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStoreType(string storeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeType);

        var normalized = string.Join(
            " ",
            storeType
                .Trim()
                .ToLowerInvariant()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));

        normalized = Regex.Replace(
            normalized,
            @"^varchar(?=\(|$)",
            "character varying");

        normalized = Regex.Replace(
            normalized,
            @"^int8$",
            "bigint");

        normalized = Regex.Replace(
            normalized,
            @"^int4$",
            "integer");

        normalized = Regex.Replace(
            normalized,
            @"^bool$",
            "boolean");

        normalized = Regex.Replace(
            normalized,
            @"^decimal(?=\(|$)",
            "numeric");

        normalized = Regex.Replace(
            normalized,
            @"^timestamptz$",
            "timestamp with time zone");

        normalized = Regex.Replace(
            normalized,
            @"^timestamp without time zone$",
            "timestamp");

        return normalized;
    }

    private static EntityHistoryProvisioningPlan BuildPlan(
        EntityHistoryDefinition definition,
        IReadOnlyCollection<EntityHistoryProvisioningStep> steps)
    {
        return new EntityHistoryProvisioningPlan
        {
            Definition = definition,
            Steps = steps
        };
    }
}
