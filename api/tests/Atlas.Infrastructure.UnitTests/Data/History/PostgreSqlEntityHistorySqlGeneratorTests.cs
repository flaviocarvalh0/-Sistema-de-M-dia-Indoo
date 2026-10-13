using Atlas.Infrastructure.Data.History.Generation;
using Atlas.Infrastructure.Data.History.Modeling;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Data.History;
public class PostgreSqlEntityHistorySqlGeneratorTests
{
    private readonly PostgreSqlEntityHistorySqlGenerator _sut = new();

    private static EntityHistoryDefinition CreateSystemParameterDefinition()
    {
        return new EntityHistoryDefinition
        {
            ClrType = typeof(Atlas.Domain.Platform.Entities.SystemParameter),
            SourceSchema = "platform",
            SourceTable = "system_parameter",
            HistorySchema = "history",
            HistoryTable = "platform_system_parameter_history",
            PrimaryKeyColumn = "id",
            PrimaryKeyStoreType = "bigint",
            Columns = new List<EntityHistoryColumnDefinition>
            {
                new()
                {
                    PropertyName = "Id",
                    ColumnName = "id",
                    StoreType = "bigint",
                    IsPrimaryKey = true,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd,
                    IsNullable = false
                },
                new()
                {
                    PropertyName = "Key",
                    ColumnName = "key",
                    StoreType = "character varying(100)",
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                    IsNullable = false
                },
                new()
                {
                    PropertyName = "Value",
                    ColumnName = "value",
                    StoreType = "text",
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                    IsNullable = false
                },
                new()
                {
                    PropertyName = "Description",
                    ColumnName = "description",
                    StoreType = "text",
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                    IsNullable = false
                },
                new()
                {
                    PropertyName = "Group",
                    ColumnName = "group",
                    StoreType = "text",
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                    IsNullable = false
                },
                new()
                {
                    PropertyName = "DataType",
                    ColumnName = "data_type",
                    StoreType = "text",
                    IsPrimaryKey = false,
                    IsShadowProperty = false,
                    ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                    IsNullable = false
                }
            }
        };
    }

    [Fact]
    public void GenerateEnsureSchemaSql_DeveGerarComandoParaCriarSchemaHistory()
    {
        // Act
        var sql = _sut.GenerateEnsureSchemaSql();

        // Assert
        sql.Should().Contain("CREATE SCHEMA IF NOT EXISTS \"history\";");
    }

    [Fact]
    public void GenerateCreateHistoryTableSql_DeveGerarEstruturaEsperada()
    {
        // Arrange
        var definition = CreateSystemParameterDefinition();

        // Act
        var sql = _sut.GenerateCreateHistoryTableSql(definition);

        // Assert
        sql.Should().Contain("CREATE TABLE IF NOT EXISTS \"history\".\"platform_system_parameter_history\"");
        sql.Should().Contain("\"history_id\" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY");
        sql.Should().Contain("\"record_id\" bigint NOT NULL");
        sql.Should().Contain("\"key\" character varying(100) NULL");
        sql.Should().Contain("\"value\" text NULL");
        sql.Should().Contain("REFERENCES \"platform\".\"system_parameter\" (\"id\")");
        sql.Should().Contain("ON DELETE CASCADE");
        sql.Should().NotContain("\"xmin\"");
    }

    [Fact]
    public void GenerateCreateOrReplaceFunctionSql_DeveCopiarSnapshot()
    {
        // Arrange
        var definition = CreateSystemParameterDefinition();

        // Act
        var sql = _sut.GenerateCreateOrReplaceFunctionSql(definition);

        // Assert
        sql.Should().Contain("CREATE OR REPLACE FUNCTION \"history\".\"capture_platform_system_parameter_history\"()");
        sql.Should().Contain("RETURNS trigger");
        sql.Should().Contain("LANGUAGE plpgsql");
        sql.Should().Contain("FROM \"platform\".\"entity_metadata\"");
        sql.Should().Contain("\"enable_history\" = TRUE");
        sql.Should().Contain("NEW.\"id\"");
        sql.Should().Contain("NEW.\"key\"");
        sql.Should().Contain("NEW.\"value\"");
        sql.Should().NotContain("NEW.\"xmin\"");
    }

    [Fact]
    public void GenerateCreateTriggerSql_DeveGerarTriggerDeInclusaoEAtualizacao()
    {
        // Arrange
        var definition = CreateSystemParameterDefinition();

        // Act
        var sql = _sut.GenerateCreateTriggerSql(definition);

        // Assert
        sql.Should().Contain("DROP TRIGGER IF EXISTS \"trg_platform_system_parameter_history\"");
        sql.Should().Contain("CREATE TRIGGER \"trg_platform_system_parameter_history\"");
        sql.Should().Contain("AFTER INSERT OR UPDATE");
        sql.Should().Contain("ON \"platform\".\"system_parameter\"");
        sql.Should().Contain("EXECUTE FUNCTION \"history\".\"capture_platform_system_parameter_history\"()");
    }

    [Fact]
    public void GenerateDropTriggerSql_DeveGerarComandoParaRemoverTrigger()
    {
        // Arrange
        var definition = CreateSystemParameterDefinition();

        // Act
        var sql = _sut.GenerateDropTriggerSql(definition);

        // Assert
        sql.Should().Contain("DROP TRIGGER IF EXISTS \"trg_platform_system_parameter_history\"");
        sql.Should().Contain("ON \"platform\".\"system_parameter\"");
    }

    [Fact]
    public void GenerateCreateIndexesSql_DeveGerarIndiceCompostoParaBuscaPerformática()
    {
        // Arrange
        var definition = CreateSystemParameterDefinition();

        // Act
        var sql = _sut.GenerateCreateIndexesSql(definition);

        // Assert
        sql.Should().Contain("CREATE INDEX IF NOT EXISTS");
        sql.Should().Contain("ON \"history\".\"platform_system_parameter_history\"");
        sql.Should().Contain("\"record_id\"");
        sql.Should().Contain("\"history_created_at\" DESC");
        sql.Should().Contain("\"history_id\" DESC");
    }
}
