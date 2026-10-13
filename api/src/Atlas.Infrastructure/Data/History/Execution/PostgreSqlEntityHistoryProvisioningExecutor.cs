using System.Data;
using Atlas.Infrastructure.Data.History.Generation;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Planning;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Data.History.Execution;

/// <summary>
/// Executa de forma transacional um plano de provisionamento
/// da infraestrutura histórica.
/// </summary>
internal sealed class PostgreSqlEntityHistoryProvisioningExecutor
    : IEntityHistoryProvisioningExecutor
{
    private readonly ApplicationDbContext _context;
    private readonly IEntityHistorySqlGenerator _sqlGenerator;
    private readonly ILogger<PostgreSqlEntityHistoryProvisioningExecutor> _logger;

    public PostgreSqlEntityHistoryProvisioningExecutor(
        ApplicationDbContext context,
        IEntityHistorySqlGenerator sqlGenerator,
        ILogger<PostgreSqlEntityHistoryProvisioningExecutor> logger)
    {
        _context = context;
        _sqlGenerator = sqlGenerator;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        EntityHistoryProvisioningPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.HasChanges)
        {
            return;
        }

        // Se já existe uma transação aberta (ex: vinda do EnableAsync no Manager)
        if (_context.Database.CurrentTransaction is not null)
        {
            await AcquireProvisioningLockAsync(cancellationToken);
            await ExecuteStepsAsync(plan, cancellationToken);
            return;
        }

        // Caso contrário, abre uma nova transação
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        try
        {
            await AcquireProvisioningLockAsync(cancellationToken);
            await ExecuteStepsAsync(plan, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ExecuteStepsAsync(
        EntityHistoryProvisioningPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (var step in plan.Steps)
        {
            var sql = GenerateSql(
                plan.Definition,
                step);

            _logger.LogDebug(
                "Executando ação histórica {HistoryAction} para {SourceSchema}.{SourceTable}: {Description}",
                step.Action,
                plan.Definition.SourceSchema,
                plan.Definition.SourceTable,
                step.Description);

            await _context.Database.ExecuteSqlRawAsync(
                sql,
                cancellationToken);
        }
    }

    private string GenerateSql(
        EntityHistoryDefinition definition,
        EntityHistoryProvisioningStep step)
    {
        return step.Action switch
        {
            EntityHistoryProvisioningAction.CreateSchema =>
                _sqlGenerator.GenerateEnsureSchemaSql(),

            EntityHistoryProvisioningAction.CreateTable =>
                _sqlGenerator.GenerateCreateHistoryTableSql(
                    definition),

            EntityHistoryProvisioningAction.AddColumn =>
                _sqlGenerator.GenerateAddColumnSql(
                    definition,
                    step.Column ??
                    throw new InvalidOperationException(
                        "A ação AddColumn exige a definição da coluna.")),

            EntityHistoryProvisioningAction.CreateIndex =>
                _sqlGenerator.GenerateCreateIndexesSql(
                    definition),

            EntityHistoryProvisioningAction.CreateForeignKey =>
                _sqlGenerator.GenerateCreateForeignKeySql(
                    definition),

            EntityHistoryProvisioningAction.CreateOrReplaceFunction =>
                _sqlGenerator.GenerateCreateOrReplaceFunctionSql(
                    definition),

            EntityHistoryProvisioningAction.CreateTrigger =>
                _sqlGenerator.GenerateCreateTriggerSql(
                    definition),

            _ => throw new ArgumentOutOfRangeException(
                nameof(step),
                step.Action,
                "Ação de provisionamento histórico não suportada.")
        };
    }

    private async Task AcquireProvisioningLockAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT pg_advisory_xact_lock(
                hashtext('atlas_entity_history_provisioning'));
            """;

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            cancellationToken);
    }
}
