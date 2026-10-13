using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Planning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Data.History.Execution;

[Collection("DatabaseCollection")]
public class PostgreSqlEntityHistoryProvisioningExecutorTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _context;
    private readonly PostgreSqlEntityHistorySchemaInspector _inspector;
    private readonly EntityHistorySchemaComparer _comparer;
    private readonly PostgreSqlEntityHistoryProvisioningExecutor _executor;
    private readonly EntityHistoryDefinition _definition;

    public PostgreSqlEntityHistoryProvisioningExecutorTests(DatabaseFixture fixture)
    {
        _context = fixture.CreateDbContext();

        _inspector = new PostgreSqlEntityHistorySchemaInspector(_context);
        _comparer = new EntityHistorySchemaComparer();

        var sqlGenerator = new PostgreSqlEntityHistorySqlGenerator();
        var resolver = new EntityHistoryModelResolver(_context);

        _executor = new PostgreSqlEntityHistoryProvisioningExecutor(
            _context,
            sqlGenerator,
            NullLogger<PostgreSqlEntityHistoryProvisioningExecutor>.Instance);

        var metadata = new EntityMetadata
        {
            Id = 1,
            TableName = "system_parameter",
            EnableHistory = true
        };

        _definition = resolver.Resolve(metadata);
    }

    public async Task InitializeAsync()
    {
        // Limpa a infraestrutura histórica antes de cada teste
        await CleanupHistoryInfrastructureAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupHistoryInfrastructureAsync();
    }

    [Fact]
    public async Task ExecuteAsync_NaoDeveExecutarNadaQuandoPlanoEstaVazio()
    {
        // Arrange
        var plan = new EntityHistoryProvisioningPlan
        {
            Definition = _definition,
            Steps = []
        };

        // Act
        await _executor.ExecuteAsync(plan);

        // Assert
        var state = await _inspector.InspectAsync(_definition);
        state.TableExists.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_DeveProvisionarInfraestruturaCompleta()
    {
        // Arrange
        var initialState = await _inspector.InspectAsync(_definition);
        var plan = _comparer.Compare(_definition, initialState);

        // Act
        await _executor.ExecuteAsync(plan);

        // Assert
        var finalState = await _inspector.InspectAsync(_definition);
        finalState.SchemaExists.Should().BeTrue();
        finalState.TableExists.Should().BeTrue();
        finalState.FunctionExists.Should().BeTrue();
        finalState.TriggerExists.Should().BeTrue();
        finalState.IndexExists.Should().BeTrue();
        finalState.ForeignKeyExists.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DeveFazerRollbackEmCasoDeErroAoExecutarSql()
    {
        // Arrange: adiciona uma coluna com tipo PostgreSQL inexistente para causar erro no DDL
        var invalidColumn = new EntityHistoryColumnDefinition
        {
            PropertyName = "InvalidProp",
            ColumnName = "invalid_col",
            StoreType = "tipo_postgres_inexistente",
            IsNullable = true,
            IsPrimaryKey = false,
            IsShadowProperty = false,
            ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never
        };

        var invalidDefinition = _definition with
        {
            Columns = [.. _definition.Columns, invalidColumn]
        };

        var initialState = await _inspector.InspectAsync(invalidDefinition);
        var plan = _comparer.Compare(invalidDefinition, initialState);

        // Act
        Func<Task> act = async () => await _executor.ExecuteAsync(plan);

        // Assert
        await act.Should().ThrowAsync<Exception>();

        // Valida que nada do plano permaneceu no banco após a exceção (Rollback)
        var finalState = await _inspector.InspectAsync(_definition);
        finalState.TableExists.Should().BeFalse();
    }

    private async Task CleanupHistoryInfrastructureAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
            DROP TRIGGER IF EXISTS trg_history_system_parameter ON public.system_parameter;
            DROP FUNCTION IF EXISTS history.fn_capture_system_parameter_history();
            DROP TABLE IF EXISTS history.platform_system_parameter_history;
        ");
    }
}
