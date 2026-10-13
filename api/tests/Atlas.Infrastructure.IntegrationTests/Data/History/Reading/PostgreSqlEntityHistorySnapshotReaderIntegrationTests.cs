using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Management;
using Atlas.Infrastructure.Data.History.Planning;
using Atlas.Infrastructure.Data.History.Reading;
using Atlas.Infrastructure.Data.Initializers.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Data.History.Reading;

[Collection("DatabaseCollection")]
public class PostgreSqlEntityHistorySnapshotReaderIntegrationTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _context;
    private readonly PostgreSqlEntityHistorySnapshotReader _reader;
    private readonly EntityHistoryModelResolver _resolver;
    private readonly EntityHistoryInitializer _initializer;
    private long _systemParameterMetadataId;
    private EntityHistoryDefinition _definition = null!;

    public PostgreSqlEntityHistorySnapshotReaderIntegrationTests(DatabaseFixture fixture)
    {
        _context = fixture.CreateDbContext();
        _reader = new PostgreSqlEntityHistorySnapshotReader(_context);
        _resolver = new EntityHistoryModelResolver(_context);

        var inspector = new PostgreSqlEntityHistorySchemaInspector(_context);
        var comparer = new EntityHistorySchemaComparer();
        var sqlGenerator = new PostgreSqlEntityHistorySqlGenerator();
        var executor = new PostgreSqlEntityHistoryProvisioningExecutor(
            _context,
            sqlGenerator,
            NullLogger<PostgreSqlEntityHistoryProvisioningExecutor>.Instance);

        var manager = new EntityHistoryManager(
            _context,
            _resolver,
            inspector,
            comparer,
            executor,
            NullLogger<EntityHistoryManager>.Instance);

        _initializer = new EntityHistoryInitializer(
            manager,
            NullLogger<EntityHistoryInitializer>.Instance);
    }

    public async Task InitializeAsync()
    {
        await CleanupInfrastructureAsync();

        var metadata = await _context.EntityMetadata
            .FirstOrDefaultAsync(m => m.TableName == "system_parameter");

        if (metadata is null)
        {
            metadata = new EntityMetadata
            {
                TableName = "system_parameter",
                EnableHistory = true
            };
            _context.EntityMetadata.Add(metadata);
        }
        else
        {
            metadata.EnableHistory = true;
        }

        await _context.SaveChangesAsync();
        _systemParameterMetadataId = metadata.Id;
        _definition = _resolver.Resolve(metadata);

        // Garante que a infraestrutura de histórico esteja pronta para os testes
        await _initializer.EnsureHistoryInfrastructureAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupInfrastructureAsync();
        await _context.DisposeAsync();
    }

    #region Testes Unitários de Validação

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task ReadAsync_DeveRejeitarPageSizeInvalido(int pageSize)
    {
        // Act
        var action = () => _reader.ReadAsync(
            _definition,
            recordId: 1,
            pageSize: pageSize);

        // Assert
        await action.Should()
            .ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ReadAsync_DeveRejeitarRecordIdInvalido()
    {
        // Act
        var action = () => _reader.ReadAsync(
            _definition,
            recordId: 0);

        // Assert
        await action.Should()
            .ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Testes de Integração com Banco de Dados

    [Fact]
    public async Task ReadAsync_LeituraEmOrdemDecrescente_DeveRetornarSnapshotsOrdenadosEComPropriedadesCorretas()
    {
        // Arrange - Criação de 3 edições no registro
        var parameter = new SystemParameter
        {
            Key = $"test-reader-order-{Guid.NewGuid():N}",
            Value = "initial",
            Description = "Teste",
            Group = "Tests",
            DataType = "string"
        };
        _context.SystemParameters.Add(parameter);
        await _context.SaveChangesAsync();

        parameter.Value = "updated";
        await _context.SaveChangesAsync();

        parameter.Value = "captured-again";
        await _context.SaveChangesAsync();

        // Act
        var result = await _reader.ReadAsync(_definition, parameter.Id);

        // Assert - Valida contagem e ordem
        result.Snapshots.Should().HaveCount(3);
        result.Snapshots
            .Select(snapshot => snapshot.Values["value"]?.ToString())
            .Should()
            .ContainInOrder("captured-again", "updated", "initial");

        // Assert - Valida Propriedades Técnicas do snapshot mais recente
        var latest = result.Snapshots.First();
        latest.HistoryId.Should().BeGreaterThan(0);
        latest.RecordId.Should().Be(parameter.Id);
        latest.HistoryCreatedAt.Should().NotBe(default(DateTimeOffset));

        // Garante que colunas técnicas não foram vazadas no dicionário Values
        latest.Values.Should().NotContainKey("history_id");
        latest.Values.Should().NotContainKey("record_id");
        latest.Values.Should().NotContainKey("history_created_at");
    }

    [Fact]
    public async Task ReadAsync_PaginacaoComBaseline_DevePaginareAvancarCursorCorretamente()
    {
        // Arrange - Cria 4 versões do registro
        var parameter = new SystemParameter
        {
            Key = $"test-reader-paged-{Guid.NewGuid():N}",
            Value = "v1",
            Description = "Teste Paginação",
            Group = "Tests",
            DataType = "string"
        };
        _context.SystemParameters.Add(parameter);
        await _context.SaveChangesAsync();

        parameter.Value = "v2";
        await _context.SaveChangesAsync();

        parameter.Value = "v3";
        await _context.SaveChangesAsync();

        parameter.Value = "v4";
        await _context.SaveChangesAsync();

        // Act 1 - Consulta a primeira página (tamanho 2)
        var firstPage = await _reader.ReadAsync(
            _definition,
            parameter.Id,
            pageSize: 2);

        // Assert 1 - Validações da primeira página
        firstPage.Snapshots.Should().HaveCount(2);
        firstPage.ComparisonBaseline.Should().NotBeNull("pois busca N+1 registros para servir de baseline de comparação");
        firstPage.HasMore.Should().BeTrue();
        firstPage.NextCursor.Should().NotBeNull();

        // Act 2 - Consulta a segunda página utilizando o cursor retornado
        var secondPage = await _reader.ReadAsync(
            _definition,
            parameter.Id,
            beforeHistoryId: firstPage.NextCursor,
            pageSize: 2);

        // Assert 2 - Validações da segunda página (sem duplicar o item do cursor)
        secondPage.Snapshots.Should().HaveCount(2);
        secondPage.HasMore.Should().BeFalse();

        // Garante que a ordenação contínua da página 1 para a página 2 não repetiu snapshots
        var firstPageIds = firstPage.Snapshots.Select(s => s.HistoryId);
        var secondPageIds = secondPage.Snapshots.Select(s => s.HistoryId);
        firstPageIds.Should().NotIntersectWith(secondPageIds);
    }

    [Fact]
    public async Task ReadAsync_RegistroSemHistorico_DeveRetornarBatchVazio()
    {
        // Act
        var result = await _reader.ReadAsync(
            _definition,
            recordId: long.MaxValue);

        // Assert
        result.Snapshots.Should().BeEmpty();
        result.ComparisonBaseline.Should().BeNull();
        result.HasMore.Should().BeFalse();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_HistoricoRemovidoPeloCascade_DeveRetornarBatchVazioAposExcluirEntidade()
    {
        // Arrange - Cria e salva um registro com histórico
        var parameter = new SystemParameter
        {
            Key = $"test-reader-cascade-{Guid.NewGuid():N}",
            Value = "initial",
            Description = "Teste Cascade Exclusão",
            Group = "Tests",
            DataType = "string"
        };
        _context.SystemParameters.Add(parameter);
        await _context.SaveChangesAsync();

        // Remove a entidade pai
        _context.SystemParameters.Remove(parameter);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reader.ReadAsync(_definition, parameter.Id);

        // Assert - A constraint ON DELETE CASCADE deve ter apagado os histórico do registro
        result.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region Cleanup

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
