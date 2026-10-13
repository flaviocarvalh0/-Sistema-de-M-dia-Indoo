using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Management;
using Atlas.Infrastructure.Data.History.Planning;
using Atlas.Infrastructure.Data.Initializers.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Data.History.Initialization;

[Collection("DatabaseCollection")]
public class EntityHistoryInitializerIntegrationTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _context;
    private readonly EntityHistoryInitializer _initializer;
    private readonly PostgreSqlEntityHistorySchemaInspector _inspector;
    private readonly EntityHistoryModelResolver _resolver;
    private long _systemParameterMetadataId;

    public EntityHistoryInitializerIntegrationTests(DatabaseFixture fixture)
    {
        _context = fixture.CreateDbContext();
        _inspector = new PostgreSqlEntityHistorySchemaInspector(_context);
        _resolver = new EntityHistoryModelResolver(_context);

        var comparer = new EntityHistorySchemaComparer();
        var sqlGenerator = new PostgreSqlEntityHistorySqlGenerator();
        var executor = new PostgreSqlEntityHistoryProvisioningExecutor(
            _context,
            sqlGenerator,
            NullLogger<PostgreSqlEntityHistoryProvisioningExecutor>.Instance);

        var manager = new EntityHistoryManager(
            _context,
            _resolver,
            _inspector,
            comparer,
            executor,
            NullLogger<EntityHistoryManager>.Instance);

        _initializer = new EntityHistoryInitializer(
                manager,
                NullLogger<EntityHistoryInitializer>.Instance);
    }

    public async Task InitializeAsync()
    {
        // Limpa estado anterior antes de cada teste
        await CleanupInfrastructureAsync();

        var metadata = await _context.EntityMetadata
            .FirstOrDefaultAsync(m => m.TableName == "system_parameter");

        if (metadata is null)
        {
            metadata = new EntityMetadata
            {
                TableName = "system_parameter",
                EnableHistory = false
            };
            _context.EntityMetadata.Add(metadata);
            await _context.SaveChangesAsync();
        }
        else
        {
            metadata.EnableHistory = false;
            await _context.SaveChangesAsync();
        }

        _systemParameterMetadataId = metadata.Id;
    }

    public async Task DisposeAsync()
    {
        await CleanupInfrastructureAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task EnsureHistoryInfrastructureAsync_EntidadeHabilitada_DeveCriarInfraestruturaCompleta()
    {
        // Arrange
        await SetEntityHistoryStateAsync(enableHistory: true);
        var definition = GetDefinition();

        // Act
        await _initializer.EnsureHistoryInfrastructureAsync();

        // Assert
        var state = await _inspector.InspectAsync(definition);
        state.TableExists.Should().BeTrue();
        state.FunctionExists.Should().BeTrue();
        state.TriggerExists.Should().BeTrue();
        state.IndexExists.Should().BeTrue();
        state.ForeignKeyExists.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureHistoryInfrastructureAsync_ExecucaoRepetida_DeveSerIdempotenteEManterApenasUmaTrigger()
    {
        // Arrange
        await SetEntityHistoryStateAsync(enableHistory: true);

        // Act - Executa duas vezes seguidas
        await _initializer.EnsureHistoryInfrastructureAsync();
        await _initializer.EnsureHistoryInfrastructureAsync();

        // Assert - Garante que a idempotência manteve apenas 1 trigger
        var triggerCount = await GetTriggerCountAsync("platform", "system_parameter", "trg_platform_system_parameter_history");
        triggerCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureHistoryInfrastructureAsync_EntidadeDesabilitadaSemInfraestrutura_NaoDeveCriarNada()
    {
        // Arrange
        await SetEntityHistoryStateAsync(enableHistory: false);
        var definition = GetDefinition();

        // Act
        await _initializer.EnsureHistoryInfrastructureAsync();

        // Assert
        var state = await _inspector.InspectAsync(definition);
        state.TableExists.Should().BeFalse();
        state.TriggerExists.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureHistoryInfrastructureAsync_EntidadeDesabilitadaComInfraestruturaExistente_DeveManterInfraEEvitarCaptura()
    {
        // Arrange
        // 1. Habilita e provisiona
        await SetEntityHistoryStateAsync(enableHistory: true);
        await _initializer.EnsureHistoryInfrastructureAsync();

        // 2. Desabilita no metadata
        await SetEntityHistoryStateAsync(enableHistory: false);

        // Act - Executa novamente o inicializador com a entidade desabilitada
        await _initializer.EnsureHistoryInfrastructureAsync();

        // Assert - Infraestrutura fisica permanece no PostgreSQL
        var definition = GetDefinition();
        var state = await _inspector.InspectAsync(definition);
        state.TableExists.Should().BeTrue();
        state.TriggerExists.Should().BeTrue();

        // Valida que uma atualização posterior NÃO gera novos snapshots
        var parameter = new SystemParameter
        {
            Key = $"test-disabled-{Guid.NewGuid():N}",
            Value = "initial",
            Description = "Teste",
            Group = "Tests",
            DataType = "string"
        };
        _context.SystemParameters.Add(parameter);
        await _context.SaveChangesAsync();

        // Atualização posterior
        parameter.Value = "updated-while-disabled";
        await _context.SaveChangesAsync();

        var count = await GetSnapshotCountAsync(parameter.Id);
        count.Should().Be(0, "pois a função do PostgreSQL consulta o estado do EnableHistory em tempo de execução");
    }

    [Fact]
    public async Task EnsureHistoryInfrastructureAsync_InfraestruturaIncompativel_DeveLancarExcecao()
    {
        // Arrange
        await SetEntityHistoryStateAsync(enableHistory: true);

        // Garante o schema 'history' antes de criar a tabela com tipo incompatível
        await _context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS history;");

        // Cria manualmente uma tabela de histórico corrompida ("value" com tipo INTEGER ao invés de TEXT)
        const string createCorruptedTableSql = """
            CREATE TABLE history.platform_system_parameter_history (
                history_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                record_id bigint NOT NULL,
                value integer NULL,
                operation_type text NOT NULL,
                changed_at timestamp with time zone NOT NULL,
                changed_by text NULL
            );
            """;
        await _context.Database.ExecuteSqlRawAsync(createCorruptedTableSql);

        // Act
        var action = () => _initializer.EnsureHistoryInfrastructureAsync();

        // Assert
        await action.Should()
            .ThrowAsync<EntityHistorySchemaIncompatibleException>();
    }

    #region Helpers & Cleanup

    private EntityHistoryDefinition GetDefinition()
    {
        var metadata = _context.EntityMetadata.Find(_systemParameterMetadataId)!;
        return _resolver.Resolve(metadata);
    }

    private async Task SetEntityHistoryStateAsync(bool enableHistory)
    {
        var metadata = await _context.EntityMetadata.FindAsync(_systemParameterMetadataId);
        if (metadata != null)
        {
            metadata.EnableHistory = enableHistory;
            await _context.SaveChangesAsync();
        }
    }

    private async Task<long> GetTriggerCountAsync(string schemaName, string tableName, string triggerName)
    {
        const string sql = """
            SELECT COUNT(*)::bigint
            FROM pg_catalog.pg_trigger AS trigger
            INNER JOIN pg_catalog.pg_class AS relation ON relation.oid = trigger.tgrelid
            INNER JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = @schemaName
              AND relation.relname = @tableName
              AND trigger.tgname = @triggerName
              AND NOT trigger.tgisinternal
            """;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var pSchema = command.CreateParameter();
        pSchema.ParameterName = "schemaName";
        pSchema.Value = schemaName;
        command.Parameters.Add(pSchema);

        var pTable = command.CreateParameter();
        pTable.ParameterName = "tableName";
        pTable.Value = tableName;
        command.Parameters.Add(pTable);

        var pTrigger = command.CreateParameter();
        pTrigger.ParameterName = "triggerName";
        pTrigger.Value = triggerName;
        command.Parameters.Add(pTrigger);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private async Task<long> GetSnapshotCountAsync(long recordId)
    {
        const string sql = """
            SELECT COUNT(*)::bigint
            FROM history.platform_system_parameter_history
            WHERE record_id = @recordId
            """;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var pRecordId = command.CreateParameter();
        pRecordId.ParameterName = "recordId";
        pRecordId.Value = recordId;
        command.Parameters.Add(pRecordId);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private async Task CleanupInfrastructureAsync()
    {
        var cleanupSql = """
            DROP TRIGGER IF EXISTS trg_platform_system_parameter_history ON platform.system_parameter;
            DROP FUNCTION IF EXISTS history.capture_platform_system_parameter_history();
            DROP TABLE IF EXISTS history.platform_system_parameter_history CASCADE;
            """;

        await _context.Database.ExecuteSqlRawAsync(cleanupSql);
    }

    #endregion
}
