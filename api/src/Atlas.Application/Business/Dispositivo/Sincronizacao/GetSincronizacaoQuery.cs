using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Clima;
using Atlas.Application.Business.Diretorio;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.Extensions.Logging;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Dispositivo.Sincronizacao;

/// <summary>
/// A grade da TV. Vai a lista de tudo que ainda pode tocar — não só o de hoje:
/// cada item traz os dias da semana e o período de exibição para a TV
/// refiltrar sozinha a cada dia, mesmo semanas sem internet, e já baixar o
/// que começa nos próximos dias. Itens expirados e mídias inativas ficam fora.
/// </summary>
/// <remarks>
/// A TV manda a <see cref="VersaoGrade"/> que já tem: se nada mudou, a resposta
/// vem sem itens (e sem gerar URLs no storage). Se um download falhar por URL
/// vencida, a TV pede de novo sem a versão para receber URLs novas.
/// As notícias têm versão própria (<see cref="VersaoNoticias"/>), para a
/// coleta de hora em hora não reenviar a grade de anúncios. A composição da
/// fila e o clima vêm em toda resposta: a TV monta a fila a cada volta.
/// </remarks>
public record GetSincronizacaoQuery : IRequest<SincronizacaoDto>
{
    /// <summary>A versão da grade que a TV já tem; vazia para receber tudo.</summary>
    public string? VersaoGrade { get; init; }

    /// <summary>A versão das notícias que a TV já tem; vazia para receber todas.</summary>
    public string? VersaoNoticias { get; init; }

    public string? VersaoApp { get; init; }

    /// <summary>Quantas mídias a TV tem baixadas e conferidas (aparece no painel).</summary>
    public int? ItensProntos { get; init; }
}

public sealed record SincronizacaoDto
{
    /// <summary>Falso quando a <see cref="VersaoGrade"/> é a mesma que a TV mandou: <see cref="Itens"/> vem nulo.</summary>
    public bool Alterada { get; init; }

    /// <summary>Identifica o conteúdo da grade; a TV guarda e manda na próxima sincronização.</summary>
    public string VersaoGrade { get; init; } = null!;

    public DateTimeOffset GeradoEm { get; init; }

    /// <summary>Dia de hoje no fuso do parceiro (o mesmo que a TV deve usar para filtrar).</summary>
    public DateOnly Hoje { get; init; }

    public string FusoHorario { get; init; } = null!;
    public int IntervaloSincronizacaoSegundos { get; init; }
    public string NomeTela { get; init; } = null!;

    /// <summary>Token novo quando o atual está perto de vencer; a TV troca o que guardou.</summary>
    public TokenDispositivo? NovoToken { get; init; }

    public IReadOnlyList<ItemSincronizacaoDto>? Itens { get; init; }

    /// <summary>Como a TV monta a fila a cada volta do loop.</summary>
    public ComposicaoFilaDto Composicao { get; init; } = null!;

    /// <summary>Identifica o conjunto de notícias; a TV guarda e manda na próxima sincronização.</summary>
    public string VersaoNoticias { get; init; } = null!;

    /// <summary>
    /// Notícias para intercalar (mais recentes primeiro). Nulo quando a
    /// <see cref="VersaoNoticias"/> é a que a TV mandou; vazio quando a
    /// playlist não usa notícias.
    /// </summary>
    public IReadOnlyList<NoticiaSincronizacaoDto>? Noticias { get; init; }

    /// <summary>
    /// Tempo agora na cidade do estabelecimento, com a previsão dos próximos
    /// dias; nulo se a playlist não usa clima nem o cartão do tempo, ou se não
    /// há leitura recente.
    /// </summary>
    public ClimaSincronizacaoDto? Clima { get; init; }

    /// <summary>Dólar e euro (PTAX) para o cartão "Cotações"; nulo se a playlist não usa o cartão.</summary>
    public IReadOnlyList<CotacaoSincronizacaoDto>? Cotacoes { get; init; }

    /// <summary>
    /// QR code fixo do diretório de anunciantes desta TV; nulo com o diretório
    /// desligado na tela ou sem endereço público configurado na API.
    /// </summary>
    public DiretorioSincronizacaoDto? Diretorio { get; init; }
}

public sealed record DiretorioSincronizacaoDto
{
    /// <summary>Endereço da página pública (o QR code codifica este texto).</summary>
    public string Url { get; init; } = null!;

    /// <summary>Texto curto embaixo do QR code.</summary>
    public string Legenda { get; init; } = LegendaPadrao;

    public const string LegendaPadrao = "Contatos dos anunciantes";
}

public sealed record CotacaoSincronizacaoDto
{
    /// <summary><c>USD</c> ou <c>EUR</c>.</summary>
    public string Moeda { get; init; } = null!;
    public string Nome { get; init; } = null!;
    public decimal Compra { get; init; }
    public decimal Venda { get; init; }

    /// <summary>Venda contra o fechamento do dia útil anterior, em %; nulo sem referência.</summary>
    public decimal? VariacaoPercentual { get; init; }

    public DateTimeOffset CotadaEm { get; init; }
    public string? Boletim { get; init; }

    /// <summary>A TV mostra junto (dados abertos do Banco Central).</summary>
    public string Credito { get; init; } = "Fonte: Banco Central (PTAX)";
}

public sealed record DiaPrevisaoDto
{
    public DateOnly Data { get; init; }
    public decimal MinimaC { get; init; }
    public decimal MaximaC { get; init; }
    public int Codigo { get; init; }
    public string Descricao { get; init; } = null!;
    public string Icone { get; init; } = null!;
}

public sealed record QrCodeSincronizacaoDto
{
    /// <summary>O que o QR code codifica (link, tel: ou Pix copia e cola).</summary>
    public string Conteudo { get; init; } = null!;

    /// <summary>Texto curto embaixo do QR code.</summary>
    public string Legenda { get; init; } = null!;

    public PosicaoQrCode Posicao { get; init; }
    public TamanhoQrCode Tamanho { get; init; }
}

public sealed record ComposicaoFilaDto
{
    /// <summary>Uma notícia depois de cada N anúncios; 0 = sem notícias.</summary>
    public int NoticiaACadaAnuncios { get; init; }
    public int DuracaoNoticiaSegundos { get; init; }

    /// <summary>Sorteia a ordem dos anúncios a cada volta; itens com <c>posicaoFixa</c> ficam no lugar.</summary>
    public bool EmbaralharAnuncios { get; init; }

    /// <summary>
    /// Segundos de anúncios por volta (fora os informativos); 0 = todos os
    /// anúncios em toda volta. Com limite, a TV roda o carrossel com cursor (regra 10).
    /// </summary>
    public int DuracaoMaximaVoltaSegundos { get; init; }

    public bool ExibirRelogio { get; init; }
    public bool ExibirClima { get; init; }

    /// <summary>Cartões que entram no rodízio dos informativos, junto com as notícias.</summary>
    public bool CartaoTempo { get; init; }
    public bool CartaoCotacoes { get; init; }
}

public sealed record NoticiaSincronizacaoDto
{
    public long Id { get; init; }
    public string Titulo { get; init; } = null!;

    /// <summary>Crédito exigido pela licença (ex.: "Fonte: Agência Brasil"), exibido junto.</summary>
    public string Credito { get; init; } = null!;

    /// <summary>A TV descarta a notícia 48 h depois desta data, mesmo offline.</summary>
    public DateTimeOffset PublicadaEm { get; init; }

    /// <summary>Imagem (download temporário, como as mídias); nulo sem imagem.</summary>
    public Uri? Url { get; init; }
    public string? Hash { get; init; }
    public long? TamanhoBytes { get; init; }
    public string? TipoConteudo { get; init; }
}

public sealed record ClimaSincronizacaoDto
{
    public decimal TemperaturaC { get; init; }
    public decimal? MinimaC { get; init; }
    public decimal? MaximaC { get; init; }

    /// <summary>Código WMO do tempo.</summary>
    public int Codigo { get; init; }
    public string Descricao { get; init; } = null!;

    /// <summary><c>sol</c>, <c>sol-nuvem</c>, <c>nuvem</c>, <c>neblina</c>, <c>garoa</c>, <c>chuva</c>, <c>neve</c> ou <c>tempestade</c>.</summary>
    public string Icone { get; init; } = null!;

    public string? Local { get; init; }
    public DateTimeOffset AtualizadaEm { get; init; }

    /// <summary>Hoje e os próximos dias (cartão "Previsão do tempo").</summary>
    public IReadOnlyList<DiaPrevisaoDto> Dias { get; init; } = [];

    /// <summary>Crédito exigido pela licença do Open-Meteo (CC BY).</summary>
    public string Credito { get; init; } = "Clima: Open-Meteo";
}

public sealed record ItemSincronizacaoDto
{
    /// <summary>Posição no loop (menor primeiro).</summary>
    public int Ordem { get; init; }

    public long MidiaId { get; init; }
    public TipoMidia Tipo { get; init; }
    public string TipoConteudo { get; init; } = null!;

    /// <summary>Download temporário (ver <c>ValidadeUrlDownloadHoras</c>). A TV toca só o arquivo baixado.</summary>
    public Uri Url { get; init; } = null!;

    /// <summary>SHA-256 do arquivo: a TV confere depois de baixar e apaga se não bater.</summary>
    public string Hash { get; init; } = null!;

    public long TamanhoBytes { get; init; }

    /// <summary>Imagem: tempo na tela. Vídeo: duração (a TV avança no fim do vídeo).</summary>
    public int DuracaoSegundos { get; init; }

    /// <summary>Bits dos dias (domingo = 1, segunda = 2, … sábado = 64; 127 = todos).</summary>
    public int DiasSemana { get; init; }

    /// <summary>Fica nesta posição quando a composição embaralha os anúncios.</summary>
    public bool PosicaoFixa { get; init; }

    /// <summary>QR code por cima da mídia; nulo = sem QR code. A TV gera a imagem do QR.</summary>
    public QrCodeSincronizacaoDto? QrCode { get; init; }

    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }

    /// <summary>Se toca em <see cref="SincronizacaoDto.Hoje"/> (dia da semana e período).</summary>
    public bool ExibirHoje { get; init; }
}

public class GetSincronizacaoQueryHandler(
    IApplicationDbContext context,
    IDispositivoAtual dispositivoAtual,
    IArmazenamentoMidias armazenamento,
    IGeradorTokenDispositivo geradorToken,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider,
    ILogger<GetSincronizacaoQueryHandler> logger)
    : IRequestHandler<GetSincronizacaoQuery, SincronizacaoDto>
{
    /// <summary>Quantas notícias a TV recebe (e baixa) no máximo.</summary>
    public const int MaximoNoticias = 20;

    public async Task<SincronizacaoDto> Handle(GetSincronizacaoQuery request, CancellationToken cancellationToken)
    {
        var dispositivo = await dispositivoAtual.ObterAsync(cancellationToken);
        var agora = timeProvider.GetUtcNow();

        var tela = await context.Telas
            .AsNoTracking()
            .Where(t => t.Id == dispositivo.TelaId)
            .Select(t => new
            {
                t.Nome,
                t.PlaylistId,
                t.ParceiroId,
                t.DiretorioAtivo,
                t.CodigoDiretorio,
                PlaylistAtiva = t.Playlist != null && t.Playlist.Ativo,
                t.Parceiro.FusoHorario,
                Composicao = t.Playlist == null
                    ? null
                    : new ComposicaoFilaDto
                    {
                        NoticiaACadaAnuncios = t.Playlist.NoticiaACadaAnuncios,
                        DuracaoNoticiaSegundos = t.Playlist.DuracaoNoticiaSegundos,
                        EmbaralharAnuncios = t.Playlist.EmbaralharAnuncios,
                        DuracaoMaximaVoltaSegundos = t.Playlist.DuracaoMaximaVoltaSegundos,
                        ExibirRelogio = t.Playlist.ExibirRelogio,
                        ExibirClima = t.Playlist.ExibirClima,
                        CartaoTempo = t.Playlist.CartaoTempo,
                        CartaoCotacoes = t.Playlist.CartaoCotacoes,
                    },
            })
            .FirstAsync(cancellationToken);

        var hoje = DateOnly.FromDateTime(FusosHorarios.NoFuso(agora, tela.FusoHorario).DateTime);

        var itens = tela.PlaylistId is { } playlistId && tela.PlaylistAtiva
            ? await context.ItensPlaylist
                .AsNoTracking()
                .Where(i => i.PlaylistId == playlistId
                    && i.Midia.Ativo
                    // Anunciante bloqueado sai de todas as grades.
                    && i.Midia.Parceiro.Situacao != SituacaoParceiro.Bloqueado
                    && (i.Midia.FimExibicao == null || i.Midia.FimExibicao >= hoje))
                .OrderBy(i => i.Ordem)
                .Select(i => new ItemGrade(
                    i.Ordem,
                    i.MidiaId,
                    i.Midia.Tipo,
                    i.Midia.TipoConteudo,
                    i.Midia.ChaveArmazenamento,
                    i.Midia.Hash,
                    i.Midia.TamanhoBytes,
                    i.Midia.DuracaoSegundos,
                    i.DiasSemana,
                    i.Midia.InicioExibicao,
                    i.Midia.FimExibicao,
                    i.PosicaoFixa,
                    i.Midia.QrTipo,
                    i.Midia.QrConteudo,
                    i.Midia.QrLegenda,
                    i.Midia.QrPosicao,
                    i.Midia.QrTamanho))
                .ToListAsync(cancellationToken)
            : [];

        var composicao = tela.PlaylistAtiva && tela.Composicao is not null
            ? tela.Composicao
            : new ComposicaoFilaDto { DuracaoNoticiaSegundos = Playlist.DuracaoNoticiaPadraoSegundos };

        var versaoGrade = CalcularVersao(tela.PlaylistId, itens);
        var alterada = !string.Equals(request.VersaoGrade, versaoGrade, StringComparison.Ordinal);

        var noticias = composicao.NoticiaACadaAnuncios > 0 ? await ListarNoticiasAsync(agora, cancellationToken) : [];
        var versaoNoticias = CalcularVersaoNoticias(noticias);
        var noticiasAlteradas = !string.Equals(request.VersaoNoticias, versaoNoticias, StringComparison.Ordinal);

        await RegistrarSincronizacaoAsync(dispositivo.TelaId, agora, request, versaoGrade, cancellationToken);

        return new SincronizacaoDto
        {
            Alterada = alterada,
            VersaoGrade = versaoGrade,
            GeradoEm = agora,
            Hoje = hoje,
            FusoHorario = tela.FusoHorario,
            IntervaloSincronizacaoSegundos = (int)configuracao.IntervaloSincronizacao.TotalSeconds,
            NomeTela = tela.Nome,
            NovoToken = PrecisaRenovar(dispositivo, agora)
                ? geradorToken.Gerar(dispositivo.TelaId, dispositivo.ParceiroId, dispositivo.DispositivoId, dispositivo.VersaoCredencial)
                : null,
            Itens = alterada ? await MontarItensAsync(itens, hoje, cancellationToken) : null,
            Composicao = composicao,
            VersaoNoticias = versaoNoticias,
            Noticias = noticiasAlteradas ? await MontarNoticiasAsync(noticias, cancellationToken) : null,
            Clima = composicao.ExibirClima || composicao.CartaoTempo
                ? await ObterClimaAsync(tela.ParceiroId, agora, hoje, cancellationToken)
                : null,
            Cotacoes = composicao.CartaoCotacoes ? await ListarCotacoesAsync(agora, cancellationToken) : null,
            Diretorio = tela.DiretorioAtivo && EnderecoDiretorio.Montar(configuracao, tela.CodigoDiretorio) is { } urlDiretorio
                ? new DiretorioSincronizacaoDto { Url = urlDiretorio.AbsoluteUri }
                : null,
        };
    }

    /// <summary>
    /// As mais recentes das fontes ativas, ainda dentro da validade. A mesma
    /// manchete em duas fontes (o feed geral e o de uma editoria) vai uma vez
    /// só, de preferência a que tem foto.
    /// </summary>
    private async Task<List<NoticiaGrade>> ListarNoticiasAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var limite = agora - Noticia.Validade;

        var candidatas = await context.Noticias
            .AsNoTracking()
            .Where(n => n.Fonte.Ativo && n.PublicadaEm >= limite)
            .OrderByDescending(n => n.PublicadaEm)
            .ThenByDescending(n => n.Id)
            .Take(MaximoNoticias * 3)
            .Select(n => new NoticiaGrade(
                n.Id, n.Titulo, n.Fonte.Credito, n.PublicadaEm, n.ChaveImagem, n.HashImagem, n.TamanhoImagemBytes, n.TipoConteudoImagem))
            .ToListAsync(cancellationToken);

        return candidatas
            .GroupBy(n => n.Titulo.Trim().ToLowerInvariant())
            .Select(g => g.OrderByDescending(n => n.Chave is not null).ThenByDescending(n => n.PublicadaEm).First())
            .OrderByDescending(n => n.PublicadaEm)
            .ThenByDescending(n => n.Id)
            .Take(MaximoNoticias)
            .ToList();
    }

    private async Task<IReadOnlyList<NoticiaSincronizacaoDto>> MontarNoticiasAsync(
        List<NoticiaGrade> noticias,
        CancellationToken cancellationToken)
    {
        var chaves = noticias.Where(n => n.Chave is not null).Select(n => n.Chave!).Distinct().ToList();
        var urls = chaves.Count > 0
            ? await armazenamento.GerarUrlsLeituraAsync(chaves, configuracao.ValidadeUrlDownload, cancellationToken)
            : new Dictionary<string, Uri>();

        return noticias.Select(n =>
        {
            // Imagem que sumiu do storage: a notícia vai só com o título.
            var url = n.Chave is { } chave && urls.TryGetValue(chave, out var u) ? u : null;

            return new NoticiaSincronizacaoDto
            {
                Id = n.Id,
                Titulo = n.Titulo,
                Credito = n.Credito,
                PublicadaEm = n.PublicadaEm,
                Url = url,
                Hash = url is null ? null : n.Hash,
                TamanhoBytes = url is null ? null : n.TamanhoBytes,
                TipoConteudo = url is null ? null : n.TipoConteudo,
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<CotacaoSincronizacaoDto>> ListarCotacoesAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var limite = agora - Cotacao.Validade;
        var cotacoes = await context.Cotacoes.AsNoTracking().Where(c => c.CotadaEm >= limite).ToListAsync(cancellationToken);

        return Cotacao.Moedas
            .Select(m => (Moeda: m, Cotacao: cotacoes.FirstOrDefault(c => c.Moeda == m.Codigo)))
            .Where(x => x.Cotacao is not null)
            .Select(x => new CotacaoSincronizacaoDto
            {
                Moeda = x.Moeda.Codigo,
                Nome = x.Moeda.Nome,
                Compra = x.Cotacao!.Compra,
                Venda = x.Cotacao.Venda,
                VariacaoPercentual = x.Cotacao.VariacaoPercentual,
                CotadaEm = x.Cotacao.CotadaEm,
                Boletim = x.Cotacao.Boletim,
            })
            .ToList();
    }

    private async Task<ClimaSincronizacaoDto?> ObterClimaAsync(long parceiroId, DateTimeOffset agora, DateOnly hoje, CancellationToken cancellationToken)
    {
        var limite = agora - PrevisaoClima.Validade;
        var previsao = await context.PrevisoesClima
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ParceiroId == parceiroId && p.AtualizadaEm >= limite, cancellationToken);

        if (previsao is not { TemperaturaC: { } temperatura, CodigoTempo: { } codigo, AtualizadaEm: { } atualizadaEm })
        {
            return null;
        }

        var (descricao, icone) = TempoWmo.Descrever(codigo);

        return new ClimaSincronizacaoDto
        {
            TemperaturaC = temperatura,
            MinimaC = previsao.MinimaC,
            MaximaC = previsao.MaximaC,
            Codigo = codigo,
            Descricao = descricao,
            Icone = icone,
            Local = previsao.Local,
            AtualizadaEm = atualizadaEm,
            Dias = ColetorClima.DiasDe(previsao.PrevisaoDias)
                .Where(d => d.Data >= hoje)
                .Select(d =>
                {
                    var (descricaoDia, iconeDia) = TempoWmo.Descrever(d.Codigo);
                    return new DiaPrevisaoDto
                    {
                        Data = d.Data, MinimaC = d.MinimaC, MaximaC = d.MaximaC, Codigo = d.Codigo,
                        Descricao = descricaoDia, Icone = iconeDia,
                    };
                })
                .ToList(),
        };
    }

    private async Task<IReadOnlyList<ItemSincronizacaoDto>> MontarItensAsync(
        List<ItemGrade> itens,
        DateOnly hoje,
        CancellationToken cancellationToken)
    {
        // Uma chamada ao storage para a grade inteira.
        var urls = await armazenamento.GerarUrlsLeituraAsync(
            itens.Select(i => i.Chave).Distinct().ToList(), configuracao.ValidadeUrlDownload, cancellationToken);

        var resultado = new List<ItemSincronizacaoDto>(itens.Count);
        foreach (var item in itens)
        {
            if (!urls.TryGetValue(item.Chave, out var url))
            {
                logger.LogWarning("Mídia {MidiaId} sem arquivo no storage ({Chave}); fora da grade.", item.MidiaId, item.Chave);
                continue;
            }

            resultado.Add(new ItemSincronizacaoDto
            {
                Ordem = item.Ordem,
                MidiaId = item.MidiaId,
                Tipo = item.Tipo,
                TipoConteudo = item.TipoConteudo,
                Url = url,
                Hash = item.Hash,
                TamanhoBytes = item.TamanhoBytes,
                DuracaoSegundos = item.DuracaoSegundos,
                DiasSemana = (int)item.DiasSemana,
                PosicaoFixa = item.PosicaoFixa,
                QrCode = item.QrTipo is { } tipoQr && item.QrConteudo is { } conteudoQr
                    ? new QrCodeSincronizacaoDto
                    {
                        Conteudo = conteudoQr,
                        Legenda = item.QrLegenda ?? MontadorQrCode.LegendaPadrao(tipoQr),
                        Posicao = item.QrPosicao ?? PosicaoQrCode.InferiorDireito,
                        Tamanho = item.QrTamanho ?? TamanhoQrCode.Medio,
                    }
                    : null,
                InicioExibicao = item.Inicio,
                FimExibicao = item.Fim,
                ExibirHoje = item.DiasSemana.Inclui(hoje.DayOfWeek)
                    && (item.Inicio is null || item.Inicio <= hoje)
                    && (item.Fim is null || item.Fim >= hoje),
            });
        }

        return resultado;
    }

    private async Task RegistrarSincronizacaoAsync(
        long telaId,
        DateTimeOffset agora,
        GetSincronizacaoQuery request,
        string versaoGrade,
        CancellationToken cancellationToken)
    {
        var estado = await context.EstadosTela.FirstOrDefaultAsync(e => e.TelaId == telaId, cancellationToken);

        if (estado is null)
        {
            estado = new EstadoTela { TelaId = telaId };
            context.EstadosTela.Add(estado);
        }

        estado.RegistrarSincronizacao(
            agora,
            string.IsNullOrWhiteSpace(request.VersaoApp) ? estado.VersaoApp : request.VersaoApp.Trim(),
            versaoGrade,
            request.ItensProntos);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Renova quando falta menos de um quarto da validade (ou o token não informa a sua).</summary>
    private bool PrecisaRenovar(TelaDoDispositivo dispositivo, DateTimeOffset agora) =>
        dispositivo.ExpiraEmToken is not { } expira
        || expira - agora < configuracao.ValidadeTokenDispositivo / 4;

    /// <summary>
    /// SHA-256 do que a TV precisa para tocar: muda quando muda a playlist, a
    /// ordem, os dias, o período, a duração ou o arquivo de qualquer item.
    /// </summary>
    public static string CalcularVersao(long? playlistId, IReadOnlyList<ItemGrade> itens)
    {
        var texto = new StringBuilder().Append("p:").Append(playlistId?.ToString(CultureInfo.InvariantCulture) ?? "-");

        foreach (var i in itens)
        {
            texto.Append('|')
                .Append(i.Ordem).Append(':')
                .Append(i.MidiaId).Append(':')
                .Append(i.Hash).Append(':')
                .Append((int)i.DiasSemana).Append(':')
                .Append(i.Inicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(':')
                .Append(i.Fim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(':')
                .Append(i.DuracaoSegundos).Append(':')
                .Append(i.PosicaoFixa ? 'f' : '-').Append(':')
                .Append(i.QrConteudo).Append(':')
                .Append(i.QrLegenda).Append(':')
                .Append((int?)i.QrPosicao).Append(':')
                .Append((int?)i.QrTamanho);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    /// <summary>Muda quando entra, sai ou troca a imagem de alguma notícia.</summary>
    public static string CalcularVersaoNoticias(IReadOnlyList<NoticiaGrade> noticias)
    {
        var texto = new StringBuilder("n");

        foreach (var n in noticias)
        {
            texto.Append('|').Append(n.Id).Append(':').Append(n.Hash).Append(':').Append(n.Credito);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    public sealed record NoticiaGrade(
        long Id,
        string Titulo,
        string Credito,
        DateTimeOffset PublicadaEm,
        string? Chave,
        string? Hash,
        long? TamanhoBytes,
        string? TipoConteudo);

    public sealed record ItemGrade(
        int Ordem,
        long MidiaId,
        TipoMidia Tipo,
        string TipoConteudo,
        string Chave,
        string Hash,
        long TamanhoBytes,
        int DuracaoSegundos,
        DiasSemana DiasSemana,
        DateOnly? Inicio,
        DateOnly? Fim,
        bool PosicaoFixa = false,
        TipoQrCode? QrTipo = null,
        string? QrConteudo = null,
        string? QrLegenda = null,
        PosicaoQrCode? QrPosicao = null,
        TamanhoQrCode? QrTamanho = null);
}

public class GetSincronizacaoQueryValidator : AbstractValidator<GetSincronizacaoQuery>
{
    public GetSincronizacaoQueryValidator()
    {
        RuleFor(x => x.VersaoGrade).MaximumLength(64).WithMessage("Versão da grade inválida.");
        RuleFor(x => x.VersaoNoticias).MaximumLength(64).WithMessage("Versão das notícias inválida.");
        RuleFor(x => x.VersaoApp).MaximumLength(30).WithMessage("A versão do app deve ter no máximo 30 caracteres.");
        RuleFor(x => x.ItensProntos).GreaterThanOrEqualTo(0).When(x => x.ItensProntos is not null);
    }
}
