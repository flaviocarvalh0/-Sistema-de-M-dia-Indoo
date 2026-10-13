using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data.History.Inspection;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Planning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Data.History.Planning;
public class EntityHistorySchemaComparerTests
{
    private readonly EntityHistorySchemaComparer _comparer = new();

    [Fact]
    public void Compare_DevePlanejarInfraestruturaCompletaQuandoTabelaNaoExiste()
    {
        // Arrange
        var definition = CreateDefinition();

        var state = new EntityHistoryInfrastructureState
        {
            SchemaExists = false,
            TableExists = false,
            FunctionExists = false,
            TriggerExists = false,
            IndexExists = false,
            ForeignKeyExists = false,
            Columns = []
        };

        // Act
        var plan = _comparer.Compare(definition, state);

        // Assert
        plan.Steps.Select(x => x.Action).Should().ContainInOrder(
            EntityHistoryProvisioningAction.CreateSchema,
            EntityHistoryProvisioningAction.CreateTable,
            EntityHistoryProvisioningAction.CreateIndex,
            EntityHistoryProvisioningAction.CreateOrReplaceFunction,
            EntityHistoryProvisioningAction.CreateTrigger);
    }

    [Fact]
    public void Compare_DeveAtualizarSomenteFuncaoQuandoInfraestruturaEstaCompleta()
    {
        // Arrange
        var definition = CreateDefinition();
        var state = CreateCompleteState(definition);

        // Act
        var plan = _comparer.Compare(definition, state);

        // Assert
        plan.Steps.Should().ContainSingle();
        plan.Steps.Single().Action.Should().Be(
            EntityHistoryProvisioningAction.CreateOrReplaceFunction);
    }

    [Fact]
    public void Compare_DeveAdicionarColunaAntesDeAtualizarFuncao()
    {
        // Arrange
        var definition = CreateDefinitionWithDataTypeColumn();
        var state = CreateCompleteStateWithoutDataType(definition);

        // Act
        var plan = _comparer.Compare(definition, state);

        // Assert
        plan.Steps.Select(x => x.Action).Should().ContainInOrder(
            EntityHistoryProvisioningAction.AddColumn,
            EntityHistoryProvisioningAction.CreateOrReplaceFunction);
    }

    [Fact]
    public void Compare_DeveBloquearTipoIncompativel()
    {
        // Arrange
        var definition = CreateDefinition();

        var state = CreateCompleteState(
            definition,
            overrideColumn: new EntityHistoryDatabaseColumn
            {
                Name = "value",
                StoreType = "integer",
                IsNullable = true
            });

        // Act
        var action = () => _comparer.Compare(definition, state);

        // Assert
        action.Should()
            .Throw<EntityHistorySchemaIncompatibleException>()
            .WithMessage("*value*integer*text*");
    }

    [Fact]
    public void Compare_NaoDeveRemoverColunaHistoricaLegada()
    {
        // Arrange
        var definition = CreateDefinition();

        var state = CreateCompleteState(
            definition,
            additionalColumn: new EntityHistoryDatabaseColumn
            {
                Name = "legacy_column",
                StoreType = "text",
                IsNullable = true
            });

        // Act
        var plan = _comparer.Compare(definition, state);

        // Assert
        plan.Steps.Should()
            .NotContain(x =>
                x.Action == EntityHistoryProvisioningAction.AddColumn &&
                x.Column != null &&
                x.Column.ColumnName == "legacy_column");
    }

    #region Helpers

    private static EntityHistoryDefinition CreateDefinition()
    {
        return new EntityHistoryDefinition
        {
            ClrType = typeof(SystemParameter),
            PrimaryKeyColumn = "id",
            PrimaryKeyStoreType = "bigint",
            SourceSchema = "platform",
            SourceTable = "system_parameter",
            HistorySchema = "history",
            HistoryTable = "platform_system_parameter_history",
            Columns = new[]
            {
                new EntityHistoryColumnDefinition
                {
                    PropertyName = "Id",
                    ColumnName = "record_id",
                    StoreType = "bigint",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsShadowProperty = false,
                    ValueGenerated = ValueGenerated.OnAdd
                },
                new EntityHistoryColumnDefinition
                {
                    PropertyName = "Value",
                    ColumnName = "value",
                    StoreType = "text",
                    IsNullable = true,
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = ValueGenerated.Never
                }
            }
        };
    }

    private static EntityHistoryDefinition CreateDefinitionWithDataTypeColumn()
    {
        var definition = CreateDefinition();
        var columnsList = definition.Columns.ToList();

        columnsList.Add(new EntityHistoryColumnDefinition
        {
            PropertyName = "DataType",
            ColumnName = "data_type",
            StoreType = "varchar(50)",
            IsNullable = true,
            IsPrimaryKey = false,
            IsShadowProperty = false,
            ValueGenerated = ValueGenerated.Never
        });

        return new EntityHistoryDefinition
        {
            ClrType = definition.ClrType,
            PrimaryKeyColumn = definition.PrimaryKeyColumn,
            PrimaryKeyStoreType = definition.PrimaryKeyStoreType,
            SourceSchema = definition.SourceSchema,
            SourceTable = definition.SourceTable,
            HistorySchema = definition.HistorySchema,
            HistoryTable = definition.HistoryTable,
            Columns = columnsList
        };
    }

    private static EntityHistoryInfrastructureState CreateCompleteState(
        EntityHistoryDefinition definition,
        EntityHistoryDatabaseColumn? overrideColumn = null,
        EntityHistoryDatabaseColumn? additionalColumn = null)
    {
        var columns = new List<EntityHistoryDatabaseColumn>
        {
            new() { Name = "history_id", StoreType = "bigint", IsNullable = false },
            new() { Name = "record_id", StoreType = definition.PrimaryKeyStoreType, IsNullable = false },
            new() { Name = "history_created_at", StoreType = "timestamp with time zone", IsNullable = false }
        };

        foreach (var col in definition.Columns.Where(c => !c.IsPrimaryKey))
        {
            if (overrideColumn != null && overrideColumn.Name.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase))
            {
                columns.Add(overrideColumn);
            }
            else
            {
                columns.Add(new EntityHistoryDatabaseColumn
                {
                    Name = col.ColumnName,
                    StoreType = col.StoreType,
                    IsNullable = true
                });
            }
        }

        if (additionalColumn != null)
        {
            columns.Add(additionalColumn);
        }

        return new EntityHistoryInfrastructureState
        {
            SchemaExists = true,
            TableExists = true,
            FunctionExists = true,
            TriggerExists = true,
            IndexExists = true,
            ForeignKeyExists = true,
            Columns = columns
        };
    }

    private static EntityHistoryInfrastructureState CreateCompleteStateWithoutDataType(EntityHistoryDefinition definition)
    {
        var state = CreateCompleteState(definition);

        // Remove a coluna data_type do estado para simular que ela ainda não existe no banco
        return new EntityHistoryInfrastructureState
        {
            SchemaExists = state.SchemaExists,
            TableExists = state.TableExists,
            FunctionExists = state.FunctionExists,
            TriggerExists = state.TriggerExists,
            IndexExists = state.IndexExists,
            ForeignKeyExists = state.ForeignKeyExists,
            Columns = state.Columns.Where(c => !c.Name.Equals("data_type", StringComparison.OrdinalIgnoreCase)).ToList()
        };
    }

    #endregion
}
