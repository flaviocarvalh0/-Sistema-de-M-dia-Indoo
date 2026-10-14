using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Clima;
using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Conteudo;

/// <summary>
/// Coleta em segundo plano: notícias (padrão a cada hora) e clima (a cada
/// 30 min). Com mais de uma instância da API, uma trava do PostgreSQL
/// (<c>pg_try_advisory_lock</c>) deixa só uma coletar de cada vez.
/// </summary>
/// <remarks>
/// A primeira coleta acontece logo depois da subida (com um pequeno atraso
/// para a aplicação terminar de iniciar). Falhas só geram log: a TV continua
/// com o que já recebeu.
/// </remarks>
internal sealed class ColetorConteudoExterno(
    IServiceScopeFactory scopes,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<ColetorConteudoExterno> logger) : BackgroundService
{
    /// <summary>Chaves das travas (números fixos do produto).</summary>
    private const long TravaNoticias = 7_310_001;
    private const long TravaClima = 7_310_002;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracao.ColetaAutomatica)
        {
            logger.LogInformation("Coleta automática de notícias e clima desligada (Indoor:Coleta:Ativa=false).");
            return;
        }

        DateTimeOffset? ultimaNoticias = null;
        DateTimeOffset? ultimaClima = null;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), timeProvider, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var agora = timeProvider.GetUtcNow();

                if (ultimaNoticias is null || agora - ultimaNoticias >= configuracao.IntervaloColetaNoticias)
                {
                    ultimaNoticias = agora;
                    await ExecutarComTravaAsync(TravaNoticias, "notícias",
                        sp => sp.GetRequiredService<ColetorNoticias>().ColetarTodasAsync(stoppingToken), stoppingToken);
                }

                if (ultimaClima is null || agora - ultimaClima >= configuracao.IntervaloColetaClima)
                {
                    ultimaClima = agora;
                    await ExecutarComTravaAsync(TravaClima, "clima",
                        sp => sp.GetRequiredService<ColetorClima>().AtualizarTodosAsync(stoppingToken), stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando.
        }
    }

    private async Task ExecutarComTravaAsync(long trava, string nome, Func<IServiceProvider, Task> coleta, CancellationToken stoppingToken)
    {
        try
        {
            await using var escopo = scopes.CreateAsyncScope();
            var context = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await context.Database.OpenConnectionAsync(stoppingToken);
            try
            {
                var obtida = await context.Database
                    .SqlQuery<bool>($"SELECT pg_try_advisory_lock({trava}) AS \"Value\"")
                    .SingleAsync(stoppingToken);

                if (!obtida)
                {
                    logger.LogDebug("Coleta de {Nome} em andamento em outra instância.", nome);
                    return;
                }

                try
                {
                    await coleta(escopo.ServiceProvider);
                }
                finally
                {
                    await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({trava})", CancellationToken.None);
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Falha na coleta de {Nome}.", nome);
        }
    }
}
