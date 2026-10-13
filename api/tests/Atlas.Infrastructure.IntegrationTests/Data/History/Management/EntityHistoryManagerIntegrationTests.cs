using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Management;
using Atlas.Infrastructure.Data.History.Planning;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Atlas.Infrastructure.IntegrationTests.Data.History.Management;

[Collection("DatabaseCollection")]
public class EntityHistoryManagerIntegrationTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _context;
    private readonly EntityHistoryManager _manager;
    private long _systemParameterMetadataId;

    public EntityHistoryManagerIntegrationTests(DatabaseFixture fixture)
    {
        _context = fixture.CreateDbContext();

        var modelResolver = new EntityHistoryModelResolver(_context);
        var inspector = new PostgreSqlEntityHistorySchemaInspector(_context);
        var comparer = new EntityHistorySchemaComparer();
        var sqlGenerator = new PostgreSqlEntityHistorySqlGenerator();
        var executor = new PostgreSqlEntityHistoryProvisioningExecutor(_context, sqlGenerator, new NullLogger<PostgreSqlEntityHistoryProvisioningExecutor>());

        _manager = new EntityHistoryManager(
            _context,
            modelResolver,
            inspector,
            comparer,
            executor,
            new NullLogger<EntityHistoryManager>());
    }

    public async Task InitializeAsync()
    {
        // Garante que o metadado do SystemParameter existe na base de teste
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

        _systemParameterMetadataId = metadata.Id;
    }

    public async Task DisposeAsync()
    {
        // Limpa a tabela de histórico e desabilita após os testes
        await _manager.DisableAsync(_systemParameterMetadataId);
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE history.platform_system_parameter_history CASCADE;");
    }

    [Fact]
    public async Task CicloDeVidaDoHistorico_DeveCapturarEIgnorarConformeEstadoDoMetadata()
    {
        // 1. Habilita o histórico e provisiona a infraestrutura
        await _manager.EnableAsync(_systemParameterMetadataId);

        // 2. Inclusão (INSERT) -> Deve gerar o 1º Snapshot
        var parameter = new SystemParameter
        {
            Key = $"history-test-{Guid.NewGuid():N}",
            Value = "initial",
            Description = "Teste de histórico",
            Group = "Tests",
            DataType = "string"
        };

        _context.SystemParameters.Add(parameter);
        await _context.SaveChangesAsync();

        var countV1 = await GetSnapshotCountAsync(parameter.Id);
        countV1.Should().Be(1);

        // 3. Atualização (UPDATE) com Histórico Ativo -> Deve gerar o 2º Snapshot
        parameter.Value = "updated";
        await _context.SaveChangesAsync();

        var countV2 = await GetSnapshotCountAsync(parameter.Id);
        countV2.Should().Be(2);

        // 4. Desabilitação do Histórico -> Não deve gerar novos Snapshots
        await _manager.DisableAsync(_systemParameterMetadataId);

        parameter.Value = "ignored";
        await _context.SaveChangesAsync();

        var countAfterDisable = await GetSnapshotCountAsync(parameter.Id);
        countAfterDisable.Should().Be(2, "pois o histórico estava desabilitado no momento da alteração");

        // 5. Reabilitação do Histórico -> Deve voltar a capturar os Snapshots
        await _manager.EnableAsync(_systemParameterMetadataId);

        parameter.Value = "captured-again";
        await _context.SaveChangesAsync();

        var countAfterReenable = await GetSnapshotCountAsync(parameter.Id);
        countAfterReenable.Should().Be(3);

        // 6. Exclusão da Entidade (DELETE) -> Na regra do motor, o parâmetro é removido ou mantido histórico conforme regra
        _context.SystemParameters.Remove(parameter);
        await _context.SaveChangesAsync();

        var countAfterDelete = await GetSnapshotCountAsync(parameter.Id);
        countAfterDelete.Should().Be(0, "pois a exclusão do registro removeu ou limpou os snapshots vinculados");
    }

    private async Task<long> GetSnapshotCountAsync(long recordId)
    {
        const string sql = """
            SELECT COUNT(*) AS "Value"
            FROM history.platform_system_parameter_history
            WHERE record_id = @recordId
            """;

        var parameter = new NpgsqlParameter("recordId", recordId);

        return await _context.Database
            .SqlQueryRaw<long>(sql, parameter)
            .SingleAsync();
    }
}
