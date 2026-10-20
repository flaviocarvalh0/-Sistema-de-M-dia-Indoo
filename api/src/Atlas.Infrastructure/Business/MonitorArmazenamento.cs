using Atlas.Application.Business.Indoor.Armazenamento;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business;

/// <summary>Inventário do storage de hora em hora (ADR-025; uma instância por vez).</summary>
internal sealed class MonitorArmazenamento(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<MonitorArmazenamento> logger) : BackgroundService
{
    private const long Trava = 7_310_005;
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), timeProvider, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await TarefaComTrava.ExecutarAsync(scopes, logger, Trava, "inventário do armazenamento",
                    sp => sp.GetRequiredService<InventarioArmazenamentoServico>().ExecutarAsync(stoppingToken), stoppingToken);
                await Task.Delay(Intervalo, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando.
        }
    }
}
