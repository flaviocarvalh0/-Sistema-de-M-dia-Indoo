namespace Atlas.Infrastructure.IntegrationTests.Data.History.Inspection;

[Collection("DatabaseCollection")]
public class PostgreSqlEntityHistorySchemaInspectorTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PostgreSqlEntityHistorySchemaInspector _inspector;
    private readonly PostgreSqlEntityHistorySqlGenerator _sqlGenerator;
    private readonly EntityHistoryDefinition _definition;

    public PostgreSqlEntityHistorySchemaInspectorTests(DatabaseFixture fixture)
    {
        // Usamos o DbContext configurado da Fixture
        _dbContext = fixture.CreateDbContext();

        _inspector = new PostgreSqlEntityHistorySchemaInspector(_dbContext);
        _sqlGenerator = new PostgreSqlEntityHistorySqlGenerator();
        _definition = CreateTestDefinition();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await CleanupInfrastructureAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task InspectAsync_WhenHistoryInfrastructureDoesNotExist_ShouldReturnFalseForObjectsAndEmptyColumns()
    {
        // Arrange
        await CleanupInfrastructureAsync();

        // Act
        var state = await _inspector.InspectAsync(_definition);

        // Assert
        state.TableExists.Should().BeFalse();
        state.FunctionExists.Should().BeFalse();
        state.TriggerExists.Should().BeFalse();
        state.IndexExists.Should().BeFalse();
        state.ForeignKeyExists.Should().BeFalse();
        state.Columns.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_WhenHistoryInfrastructureIsCreated_ShouldReturnTrueForAllObjectsAndMapColumnsCorrectly()
    {
        // Arrange
        await CleanupInfrastructureAsync();

        // Geramos as DDLs chamando os métodos específicos da sua classe de SqlGenerator
        var ensureSchemaSql = _sqlGenerator.GenerateEnsureSchemaSql();
        var createTableSql = _sqlGenerator.GenerateCreateHistoryTableSql(_definition);
        var createIndexSql = _sqlGenerator.GenerateCreateIndexesSql(_definition);
        var createFuncSql = _sqlGenerator.GenerateCreateOrReplaceFunctionSql(_definition);
        var createTrigSql = _sqlGenerator.GenerateCreateTriggerSql(_definition);

        // Act - Executa os scripts DDL no PostgreSQL real em ordem
        await ExecuteSqlScriptAsync(ensureSchemaSql);
        await ExecuteSqlScriptAsync(createTableSql);
        await ExecuteSqlScriptAsync(createIndexSql);
        await ExecuteSqlScriptAsync(createFuncSql);
        await ExecuteSqlScriptAsync(createTrigSql);

        var state = await _inspector.InspectAsync(_definition);

        // Assert
        state.SchemaExists.Should().BeTrue();
        state.TableExists.Should().BeTrue();
        state.FunctionExists.Should().BeTrue();
        state.TriggerExists.Should().BeTrue();
        state.IndexExists.Should().BeTrue();
        state.ForeignKeyExists.Should().BeTrue();

        state.Columns.Should().Contain(c => c.Name == "history_id");
        state.Columns.Should().Contain(c => c.Name == "record_id");
        state.Columns.Should().Contain(c => c.Name == "value" && c.StoreType.StartsWith("text", StringComparison.OrdinalIgnoreCase));
    }

    #region Helpers & Cleanup

    private async Task ExecuteSqlScriptAsync(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return;
        await _dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task CleanupInfrastructureAsync()
    {
        var cleanupSql = """
            DROP TRIGGER IF EXISTS trg_platform_system_parameter_history ON platform.system_parameter;
            DROP FUNCTION IF EXISTS history.capture_platform_system_parameter_history();
            DROP TABLE IF EXISTS history.platform_system_parameter_history;
            """;

        await _dbContext.Database.ExecuteSqlRawAsync(cleanupSql);
    }

    private static EntityHistoryDefinition CreateTestDefinition()
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

    #endregion
}
