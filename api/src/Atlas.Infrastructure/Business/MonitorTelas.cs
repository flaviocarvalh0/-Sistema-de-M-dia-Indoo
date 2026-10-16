using Atlas.Application.Business.Rede.Telas.Alertas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business;

/// <summary>Varredura das TVs fora do ar a cada 5 minutos (uma instância por vez).</summary>
internal sealed class MonitorTelas(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<MonitorTelas> logger) : BackgroundService
{
    private const long Trava = 7_310_004;
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), timeProvider, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await TarefaComTrava.ExecutarAsync(scopes, logger, Trava, "alerta de TVs fora do ar",
                    sp => sp.GetRequiredService<VerificadorTelasOffline>().VerificarAsync(stoppingToken), stoppingToken);
                await Task.Delay(Intervalo, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando.
        }
    }
}
