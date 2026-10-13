using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.Extensions.Logging;

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
/// </remarks>
public record GetSincronizacaoQuery : IRequest<SincronizacaoDto>
{
    /// <summary>A versão da grade que a TV já tem; vazia para receber tudo.</summary>
    public string? VersaoGrade { get; init; }

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
                PlaylistAtiva = t.Playlist != null && t.Playlist.Ativo,
                t.Parceiro.FusoHorario,
            })
            .FirstAsync(cancellationToken);

        var hoje = DateOnly.FromDateTime(FusosHorarios.NoFuso(agora, tela.FusoHorario).DateTime);

        var itens = tela.PlaylistId is { } playlistId && tela.PlaylistAtiva
            ? await context.ItensPlaylist
                .AsNoTracking()
                .Where(i => i.PlaylistId == playlistId
                    && i.Midia.Ativo
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
                    i.Midia.FimExibicao))
                .ToListAsync(cancellationToken)
            : [];

        var versaoGrade = CalcularVersao(tela.PlaylistId, itens);
        var alterada = !string.Equals(request.VersaoGrade, versaoGrade, StringComparison.Ordinal);

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
                .Append(i.DuracaoSegundos);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

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
        DateOnly? Fim);
}

public class GetSincronizacaoQueryValidator : AbstractValidator<GetSincronizacaoQuery>
{
    public GetSincronizacaoQueryValidator()
    {
        RuleFor(x => x.VersaoGrade).MaximumLength(64).WithMessage("Versão da grade inválida.");
        RuleFor(x => x.VersaoApp).MaximumLength(30).WithMessage("A versão do app deve ter no máximo 30 caracteres.");
        RuleFor(x => x.ItensProntos).GreaterThanOrEqualTo(0).When(x => x.ItensProntos is not null);
    }
}
