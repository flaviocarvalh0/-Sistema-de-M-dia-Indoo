using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Garante o bucket privado na subida da API. Roda em segundo plano e não
/// derruba a API se o Supabase estiver fora: o erro fica no log e o envio de
/// mídia falha com mensagem clara até o storage voltar.
/// </summary>
internal sealed class InicializadorBucket(ArmazenamentoSupabase armazenamento, ILogger<InicializadorBucket> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await armazenamento.GarantirBucketAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Não foi possível conferir ou criar o bucket de mídias no Supabase.");
        }
    }
}
