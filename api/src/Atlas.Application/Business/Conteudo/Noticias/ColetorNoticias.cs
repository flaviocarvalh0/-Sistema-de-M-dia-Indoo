using System.Security.Cryptography;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Conteudo.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Conteudo.Noticias;

/// <summary>
/// Lê os feeds das fontes ativas e guarda as notícias novas, com a imagem no
/// storage (<c>noticias/{fonteId}/{hash}.jpg</c>) para a TV baixar e exibir
/// offline. Guarda as <see cref="FonteNoticia.NoticiasGuardadas"/> mais
/// recentes de cada fonte e apaga as outras (e os arquivos delas), para não
/// gastar o espaço do storage.
/// </summary>
/// <remarks>
/// Uma fonte fora do ar não atrapalha as outras: o erro fica registrado na
/// fonte (aparece no painel) e as notícias que ela já tinha continuam valendo.
/// A foto vem do feed ou, sem ela, da foto de destaque da página da notícia
/// (<see cref="PaginaNoticia"/>). Notícia sem foto entra só com o título, e a
/// foto é tentada de novo nas coletas das 24 h seguintes.
/// </remarks>
public sealed class ColetorNoticias(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    IClienteConteudoExterno cliente,
    TimeProvider timeProvider,
    ILogger<ColetorNoticias> logger)
{
    public const long TamanhoMaximoFeedBytes = 5L * 1024 * 1024;

    /// <summary>Lê todas as fontes ativas, uma de cada vez.</summary>
    public async Task ColetarTodasAsync(CancellationToken cancellationToken)
    {
        var ids = await context.FontesNoticia.Where(f => f.Ativo).Select(f => f.Id).ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await ColetarAsync(id, cancellationToken);
        }
    }

    public async Task<ResultadoColetaNoticias> ColetarAsync(long fonteId, CancellationToken cancellationToken)
    {
        var fonte = await context.FontesNoticia.FirstAsync(f => f.Id == fonteId, cancellationToken);
        var agora = timeProvider.GetUtcNow();

        IReadOnlyList<ItemFeed> itens;
        try
        {
            var feed = await cliente.BaixarAsync(new Uri(fonte.UrlFeed), TamanhoMaximoFeedBytes, cancellationToken);
            itens = LeitorFeed.Ler(feed.Bytes, new Uri(fonte.UrlFeed));
        }
        catch (Exception ex) when (ex is ConteudoExternoIndisponivelException or FormatException)
        {
            logger.LogWarning("Fonte de notícias {Fonte} ({Url}) falhou: {Erro}", fonte.Nome, fonte.UrlFeed, ex.Message);
            fonte.RegistrarColeta(agora, ex.Message);
            await context.SaveChangesAsync(cancellationToken);
            return new ResultadoColetaNoticias(0, 0, ex.Message);
        }

        var guardadas = await context.Noticias
            .Where(n => n.FonteNoticiaId == fonte.Id)
            .ToListAsync(cancellationToken);
        var existentes = guardadas.Select(n => n.Identificador).ToHashSet(StringComparer.Ordinal);

        var limite = agora - Noticia.Validade;
        var novas = itens
            .Select(i => (Item: i, Data: Normalizar(i.PublicadaEm, agora)))
            .Where(x => x.Data >= limite && !existentes.Contains(x.Item.Identificador))
            .DistinctBy(x => x.Item.Identificador)
            .OrderByDescending(x => x.Data)
            .Take(FonteNoticia.NoticiasGuardadas)
            .ToList();

        foreach (var (item, data) in novas)
        {
            var noticia = new Noticia
            {
                FonteNoticiaId = fonte.Id,
                Identificador = item.Identificador,
                Titulo = item.Titulo,
                PublicadaEm = data,
                ColetadaEm = agora,
            };

            await GuardarImagemAsync(noticia, item, cancellationToken);
            context.Noticias.Add(noticia);
        }

        // Notícias recentes que ficaram sem foto (página fora do ar, foto
        // grande demais): tenta de novo enquanto o feed ainda as traz.
        var noFeed = itens.DistinctBy(i => i.Identificador).ToDictionary(i => i.Identificador, StringComparer.Ordinal);
        foreach (var semFoto in guardadas.Where(n => n.ChaveImagem is null && n.ColetadaEm >= agora - RetentarFotoPor))
        {
            if (noFeed.TryGetValue(semFoto.Identificador, out var item))
            {
                await GuardarImagemAsync(semFoto, item, cancellationToken);
            }
        }

        fonte.RegistrarColeta(agora, erro: null);
        await context.SaveChangesAsync(cancellationToken);

        var removidas = await AplicarRetencaoAsync(fonte.Id, cancellationToken);

        if (novas.Count > 0 || removidas > 0)
        {
            logger.LogInformation("Fonte {Fonte}: {Novas} notícia(s) nova(s), {Removidas} removida(s).", fonte.Nome, novas.Count, removidas);
        }

        return new ResultadoColetaNoticias(novas.Count, removidas, null);
    }

    /// <summary>
    /// Apaga as notícias e os arquivos de uma fonte (exclusão da fonte). O
    /// arquivo só sai se nenhuma notícia de outra fonte usa a mesma imagem.
    /// </summary>
    public async Task RemoverArquivosAsync(IReadOnlyCollection<string> chaves, CancellationToken cancellationToken)
    {
        foreach (var chave in chaves.Distinct())
        {
            if (await context.Noticias.AnyAsync(n => n.ChaveImagem == chave, cancellationToken))
            {
                continue;
            }

            try
            {
                await armazenamento.RemoverAsync(chave, cancellationToken);
            }
            catch (ArmazenamentoIndisponivelException ex)
            {
                logger.LogWarning(ex, "Não foi possível apagar a imagem {Chave} do storage; fica órfã.", chave);
            }
        }
    }

    /// <summary>Por quanto tempo uma notícia sem foto ganha nova tentativa a cada coleta.</summary>
    public static readonly TimeSpan RetentarFotoPor = TimeSpan.FromHours(24);

    /// <summary>Data futura (relógio errado no feed) ou ausente vira a hora da coleta.</summary>
    private static DateTimeOffset Normalizar(DateTimeOffset? data, DateTimeOffset agora) =>
        data is { } d && d <= agora + TimeSpan.FromMinutes(10) ? d.ToUniversalTime() : agora;

    /// <summary>A foto do feed; sem ela (ou se não serve), a de destaque da página da notícia.</summary>
    private async Task GuardarImagemAsync(Noticia noticia, ItemFeed item, CancellationToken cancellationToken)
    {
        if (item.Imagem is { } doFeed && await TentarImagemAsync(noticia, doFeed, cancellationToken))
        {
            return;
        }

        if (item.Link is not { } pagina)
        {
            return;
        }

        try
        {
            var html = await cliente.BaixarAsync(pagina, PaginaNoticia.TamanhoMaximoBytes, cancellationToken, truncar: true);
            if (PaginaNoticia.ImagemDestaque(html.Bytes, pagina) is { } destaque && destaque != item.Imagem)
            {
                await TentarImagemAsync(noticia, destaque, cancellationToken);
            }
        }
        catch (ConteudoExternoIndisponivelException ex)
        {
            logger.LogDebug("Página da notícia {Titulo} não abriu: {Erro}", noticia.Titulo, ex.Message);
        }
    }

    /// <returns>Se a imagem foi guardada.</returns>
    private async Task<bool> TentarImagemAsync(Noticia noticia, Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var imagem = await cliente.BaixarAsync(url, Noticia.TamanhoMaximoImagemBytes, cancellationToken);
            var tipo = TiposArquivoMidia.DetectarImagem(imagem.Bytes.AsSpan(0, Math.Min(imagem.Bytes.Length, TiposArquivoMidia.BytesAssinatura)));

            // A TV exibe JPEG, PNG e WebP.
            if (tipo is null)
            {
                logger.LogDebug("Imagem da notícia {Titulo} ignorada: formato não aceito ({Url}).", noticia.Titulo, url);
                return false;
            }

            var hash = Convert.ToHexStringLower(SHA256.HashData(imagem.Bytes));
            var chave = $"noticias/{noticia.FonteNoticiaId}/{hash}{TiposArquivoMidia.ExtensaoImagem(tipo)}";

            await armazenamento.GravarAsync(chave, imagem.Bytes, tipo, cancellationToken);

            noticia.ChaveImagem = chave;
            noticia.TipoConteudoImagem = tipo;
            noticia.TamanhoImagemBytes = imagem.Bytes.Length;
            noticia.HashImagem = hash;
            return true;
        }
        catch (Exception ex) when (ex is ConteudoExternoIndisponivelException or ArmazenamentoIndisponivelException)
        {
            // Sem a imagem a notícia ainda vale: a TV mostra só o título.
            logger.LogDebug("Imagem da notícia {Titulo} não guardada: {Erro}", noticia.Titulo, ex.Message);
            return false;
        }
    }

    /// <summary>Fica com as mais recentes da fonte; as outras saem, com os arquivos.</summary>
    private async Task<int> AplicarRetencaoAsync(long fonteId, CancellationToken cancellationToken)
    {
        var antigas = await context.Noticias
            .Where(n => n.FonteNoticiaId == fonteId)
            .OrderByDescending(n => n.PublicadaEm)
            .ThenByDescending(n => n.Id)
            .Skip(FonteNoticia.NoticiasGuardadas)
            .ToListAsync(cancellationToken);

        if (antigas.Count == 0)
        {
            return 0;
        }

        var chaves = antigas.Where(n => n.ChaveImagem is not null).Select(n => n.ChaveImagem!).ToList();
        context.Noticias.RemoveRange(antigas);
        await context.SaveChangesAsync(cancellationToken);

        await RemoverArquivosAsync(chaves, cancellationToken);

        return antigas.Count;
    }
}

/// <summary>Notícias que entraram e saíram; <c>Erro</c> diz por que o feed não foi lido (nulo quando deu certo).</summary>
public sealed record ResultadoColetaNoticias(int Novas, int Removidas, string? Erro);
