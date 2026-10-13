using Atlas.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Data.Initializers.Platform;

// <summary>
/// Garante que todas as entidades com histórico habilitado possuam
/// tabela histórica, função, trigger, índice e relacionamento.
/// </summary>
internal sealed class EntityHistoryInitializer
    : IEntityHistoryInitializer
{
    private readonly IEntityHistoryManager _historyManager;
    private readonly ILogger<EntityHistoryInitializer> _logger;

    public EntityHistoryInitializer(
        IEntityHistoryManager historyManager,
        ILogger<EntityHistoryInitializer> logger)
    {
        _historyManager = historyManager;
        _logger = logger;
    }

    public async Task EnsureHistoryInfrastructureAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando reconciliação da infraestrutura histórica.");

        await _historyManager.EnsureEnabledHistoriesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Reconciliação da infraestrutura histórica concluída.");
    }
}
