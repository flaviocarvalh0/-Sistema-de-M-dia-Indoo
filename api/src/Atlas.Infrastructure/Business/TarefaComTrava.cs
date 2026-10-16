using Atlas.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business;

/// <summary>
/// Tarefa periódica que só uma instância da API executa por vez: uma trava do
/// PostgreSQL (<c>pg_try_advisory_lock</c>) por tarefa. Falhas só geram log.
/// </summary>
internal static class TarefaComTrava
{
    public static async Task ExecutarAsync(
        IServiceScopeFactory scopes,
        ILogger logger,
        long trava,
        string nome,
        Func<IServiceProvider, Task> tarefa,
        CancellationToken stoppingToken)
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
                    logger.LogDebug("{Nome}: em andamento em outra instância.", nome);
                    return;
                }

                try
                {
                    await tarefa(escopo.ServiceProvider);
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
            logger.LogError(ex, "Falha em {Nome}.", nome);
        }
    }
}
