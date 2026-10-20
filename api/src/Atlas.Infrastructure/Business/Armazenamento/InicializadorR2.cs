using Atlas.Application.Business.Common;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Business.Indoor.Armazenamento;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Na subida com o R2 (ADR-025): libera o CORS do bucket para o painel e, se
/// <c>Indoor:Armazenamento:CopiarDoSupabase</c> estiver ligado, copia do
/// Supabase os arquivos que o banco conhece e ainda não estão no R2. Nada
/// disso derruba a API: os erros ficam no log.
/// </summary>
internal sealed class InicializadorR2(
    ArmazenamentoR2 r2,
    IServiceScopeFactory scopes,
    IConfiguracaoIndoor configuracao,
    ILogger<InicializadorR2> logger,
    ArmazenamentoSupabase? origem = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await r2.ConfigurarCorsAsync([configuracao.UrlPainel], stoppingToken);
            logger.LogInformation("CORS do bucket R2 liberado para {Origem}.", configuracao.UrlPainel);
        }
        catch (ArmazenamentoIndisponivelException ex)
        {
            logger.LogWarning(ex,
                "Não foi possível configurar o CORS do bucket R2 (o token só tem permissão de objetos?). "
                + "Configure no painel da Cloudflare: origem {Origem}, métodos PUT/GET/HEAD (ver docs/IMPLANTACAO.md).",
                configuracao.UrlPainel);
        }

        if (origem is null)
        {
            return;
        }

        try
        {
            await CopiarDoSupabaseAsync(origem, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "A cópia do Supabase para o R2 parou. Ela recomeça (do que falta) na próxima subida.");
        }
    }

    /// <summary>
    /// Arquivo por arquivo, pulando o que já está no R2. Antes de começar,
    /// confere que o total cabe no teto: se não couber, não copia nada.
    /// </summary>
    private async Task CopiarDoSupabaseAsync(ArmazenamentoSupabase supabase, CancellationToken cancellationToken)
    {
        await using var escopo = scopes.CreateAsyncScope();
        var context = escopo.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var teto = escopo.ServiceProvider.GetRequiredService<TetoArmazenamento>();

        var uso = await teto.MedirAsync(cancellationToken);
        if (uso.ArquivosBytes > uso.LimiteBytes)
        {
            logger.LogError(
                "Cópia para o R2 cancelada: o banco soma {Total:N0} MB, acima do teto de {Limite:N0} MB. Libere espaço antes.",
                uso.ArquivosBytes / 1048576, uso.LimiteBytes / 1048576);
            return;
        }

        var chaves = new List<(string Chave, string Tipo)>();
        chaves.AddRange(await context.Midias.Select(m => new ValueTuple<string, string>(m.ChaveArmazenamento, m.TipoConteudo)).ToListAsync(cancellationToken));
        chaves.AddRange(await context.Midias.Where(m => m.PendenteChave != null)
            .Select(m => new ValueTuple<string, string>(m.PendenteChave!, m.PendenteTipoConteudo ?? m.TipoConteudo)).ToListAsync(cancellationToken));
        chaves.AddRange(await context.VersoesApp.Select(v => new ValueTuple<string, string>(v.ChaveArmazenamento, "application/vnd.android.package-archive")).ToListAsync(cancellationToken));
        chaves.AddRange(await context.Noticias.Where(n => n.ChaveImagem != null)
            .Select(n => new ValueTuple<string, string>(n.ChaveImagem!, "image/jpeg")).ToListAsync(cancellationToken));

        int copiados = 0, existentes = 0, ausentes = 0;
        foreach (var (chave, tipo) in chaves.DistinctBy(c => c.Chave))
        {
            if (await r2.ObterAsync(chave, cancellationToken) is not null)
            {
                existentes++;
                continue;
            }

            await using var leitura = await supabase.AbrirLeituraAsync(chave, cancellationToken);
            if (leitura is null)
            {
                ausentes++;
                continue;
            }

            using var memoria = new MemoryStream();
            await leitura.CopyToAsync(memoria, cancellationToken);
            await r2.GravarAsync(chave, memoria.ToArray(), tipo, cancellationToken);
            copiados++;
        }

        logger.LogInformation(
            "Cópia do Supabase para o R2: {Copiados} copiado(s), {Existentes} já estavam lá, {Ausentes} não existiam no Supabase.",
            copiados, existentes, ausentes);
    }
}
