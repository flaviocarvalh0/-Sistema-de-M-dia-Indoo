using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data.History.Execution;
using Atlas.Infrastructure.Data.History.Inspection;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Planning;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Data.History.Management;

/// <summary>
/// Coordena o provisionamento e a ativação do histórico das entidades.
/// </summary>
internal sealed class EntityHistoryManager
    : IEntityHistoryManager
{
    private readonly ApplicationDbContext _context;
    private readonly IEntityHistoryModelResolver _modelResolver;
    private readonly IEntityHistorySchemaInspector _schemaInspector;
    private readonly IEntityHistorySchemaComparer _schemaComparer;
    private readonly IEntityHistoryProvisioningExecutor _executor;
    private readonly ILogger<EntityHistoryManager> _logger;

    public EntityHistoryManager(
        ApplicationDbContext context,
        IEntityHistoryModelResolver modelResolver,
        IEntityHistorySchemaInspector schemaInspector,
        IEntityHistorySchemaComparer schemaComparer,
        IEntityHistoryProvisioningExecutor executor,
        ILogger<EntityHistoryManager> logger)
    {
        _context = context;
        _modelResolver = modelResolver;
        _schemaInspector = schemaInspector;
        _schemaComparer = schemaComparer;
        _executor = executor;
        _logger = logger;
    }

    public async Task EnsureEnabledHistoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var metadataItems = await _context.EntityMetadata
            .AsNoTracking()
            .Where(metadata => metadata.EnableHistory)
            .OrderBy(metadata => metadata.Id)
            .ToListAsync(cancellationToken);

        foreach (var metadata in metadataItems)
        {
            await EnsureInfrastructureAsync(
                metadata,
                cancellationToken);
        }
    }

    public async Task EnableAsync(
        long entityMetadataId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(
            entityMetadataId,
            cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await EnsureInfrastructureAsync(
                metadata,
                cancellationToken);

            if (!metadata.EnableHistory)
            {
                metadata.EnableHistory = true;

                await _context.SaveChangesAsync(
                    cancellationToken);
            }

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Histórico habilitado para a entidade {TableName}.",
                metadata.TableName);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    public async Task DisableAsync(
        long entityMetadataId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(
            entityMetadataId,
            cancellationToken);

        if (!metadata.EnableHistory)
        {
            return;
        }

        metadata.EnableHistory = false;

        await _context.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Histórico desabilitado para a entidade {TableName}. " +
            "A infraestrutura e os snapshots existentes foram preservados.",
            metadata.TableName);
    }

    private async Task EnsureInfrastructureAsync(
        EntityMetadata metadata,
        CancellationToken cancellationToken)
    {
        var definition =
            _modelResolver.Resolve(metadata);

        var state =
            await _schemaInspector.InspectAsync(
                definition,
                cancellationToken);

        var plan =
            _schemaComparer.Compare(
                definition,
                state);

        await _executor.ExecuteAsync(
            plan,
            cancellationToken);

        _logger.LogDebug(
            "Infraestrutura histórica reconciliada para " +
            "{SourceSchema}.{SourceTable}. Ações executadas: {ActionCount}.",
            definition.SourceSchema,
            definition.SourceTable,
            plan.Steps.Count);
    }

    private async Task<EntityMetadata> GetMetadataAsync(
        long entityMetadataId,
        CancellationToken cancellationToken)
    {
        return await _context.EntityMetadata
            .FirstOrDefaultAsync(
                metadata => metadata.Id == entityMetadataId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"O metadado de entidade com ID " +
                $"'{entityMetadataId}' não foi encontrado.");
    }
}
