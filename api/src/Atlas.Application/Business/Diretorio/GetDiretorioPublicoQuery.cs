using System.Globalization;
using System.Text.RegularExpressions;
using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Diretorio;

/// <summary>
/// A página pública de uma TV (sem login): os anunciantes que estão na grade
/// de hoje e que aceitaram aparecer no diretório, com os contatos do perfil
/// público — só eles: os QR codes dos anúncios não entram (ADR-018).
/// </summary>
/// <remarks>
/// Dupla autorização: a operadora liga o diretório na tela e cada parceiro
/// aceita aparecer. O estabelecimento que hospeda a TV vem primeiro (se
/// aceitou), mesmo sem anúncio na grade. Código inexistente, tela inativa ou
/// diretório desligado respondem 404, sem dizer qual dos três.
/// </remarks>
public sealed record GetDiretorioPublicoQuery(string Codigo) : IRequest<DiretorioPublicoDto>;

public sealed record DiretorioPublicoDto
{
    /// <summary>Nome do estabelecimento onde a TV está; nulo se ele não aceitou aparecer.</summary>
    public string? Estabelecimento { get; init; }

    public IReadOnlyList<AnuncianteDiretorioDto> Anunciantes { get; init; } = [];

    public DateTimeOffset GeradoEm { get; init; }
}

public sealed record AnuncianteDiretorioDto
{
    public string Nome { get; init; } = null!;
    public string? Categoria { get; init; }
    public string? Descricao { get; init; }

    /// <summary>É o estabelecimento que hospeda a TV (vai primeiro).</summary>
    public bool Anfitriao { get; init; }

    public IReadOnlyList<ContatoDiretorioDto> Contatos { get; init; } = [];
}

/// <param name="Tipo">Define o botão: WhatsApp, Instagram, Link (site), Telefone ou Pix.</param>
/// <param name="Rotulo">Texto do botão.</param>
/// <param name="Valor">Endereço (<c>https:</c> ou <c>tel:</c>); no Pix, o copia e cola.</param>
/// <param name="Detalhe">Para onde vai, legível: "(92) 99999-0000", "@usuario", "site.com.br".</param>
public sealed record ContatoDiretorioDto(TipoQrCode Tipo, string Rotulo, string Valor, string? Detalhe = null);

public partial class GetDiretorioPublicoQueryHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetDiretorioPublicoQuery, DiretorioPublicoDto>
{
    /// <summary>Botões por anunciante, no máximo.</summary>
    public const int MaximoContatos = 6;

    private static readonly StringComparer OrdemNomes = StringComparer.Create(new CultureInfo("pt-BR"), CompareOptions.IgnoreCase);

    public async Task<DiretorioPublicoDto> Handle(GetDiretorioPublicoQuery request, CancellationToken cancellationToken)
    {
        var codigo = NormalizarCodigo(request.Codigo);

        var tela = codigo is null
            ? null
            : await context.Telas
                .AsNoTracking()
                .Where(t => t.CodigoDiretorio == codigo && t.Ativo && t.DiretorioAtivo)
                .Select(t => new
                {
                    t.ParceiroId,
                    t.PlaylistId,
                    PlaylistAtiva = t.Playlist != null && t.Playlist.Ativo,
                    t.Parceiro.FusoHorario,
                })
                .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Codigo, tela);

        var agora = timeProvider.GetUtcNow();
        var hoje = DateOnly.FromDateTime(FusosHorarios.NoFuso(agora, tela.FusoHorario).DateTime);

        // A grade de hoje, só de quem aceitou: as mesmas regras da sincronização.
        var itens = tela.PlaylistId is { } playlistId && tela.PlaylistAtiva
            ? await context.ItensPlaylist
                .AsNoTracking()
                .Where(i => i.PlaylistId == playlistId
                    && i.Midia.Ativo
                    && i.Midia.Parceiro.Situacao != SituacaoParceiro.Bloqueado
                    && i.Midia.Parceiro.ExibirNoDiretorioPublico
                    && (i.Midia.InicioExibicao == null || i.Midia.InicioExibicao <= hoje)
                    && (i.Midia.FimExibicao == null || i.Midia.FimExibicao >= hoje))
                .OrderBy(i => i.Ordem)
                .Select(i => new { i.Midia.ParceiroId, i.DiasSemana })
                .ToListAsync(cancellationToken)
            : [];

        var deHoje = itens.Where(i => i.DiasSemana.Inclui(hoje.DayOfWeek)).ToList();
        var ids = deHoje.Select(i => i.ParceiroId).Append(tela.ParceiroId).Distinct().ToList();

        var parceiros = await context.Parceiros
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.ExibirNoDiretorioPublico && p.Situacao != SituacaoParceiro.Bloqueado)
            .Select(p => new
            {
                p.Id, p.Nome, p.Categoria, p.DescricaoPublica, p.WhatsappPublico, p.InstagramPublico, p.SitePublico,
            })
            .ToListAsync(cancellationToken);

        var cartoes = parceiros
            .Select(p =>
            {
                var contatos = new List<ContatoDiretorioDto>();
                if (p.WhatsappPublico is { } whatsapp)
                {
                    contatos.Add(new(TipoQrCode.WhatsApp, "WhatsApp", $"https://wa.me/{whatsapp}"));
                }

                if (p.InstagramPublico is { } instagram)
                {
                    contatos.Add(new(TipoQrCode.Instagram, "Instagram", $"https://instagram.com/{instagram}"));
                }

                if (p.SitePublico is { } site)
                {
                    contatos.Add(new(TipoQrCode.Link, "Site", site));
                }

                return new AnuncianteDiretorioDto
                {
                    Nome = p.Nome,
                    Categoria = p.Categoria,
                    Descricao = p.DescricaoPublica,
                    Anfitriao = p.Id == tela.ParceiroId,
                    Contatos = SemRepetir(contatos).Take(MaximoContatos).ToList(),
                };
            })
            .OrderByDescending(c => c.Anfitriao)
            .ThenBy(c => c.Nome, OrdemNomes)
            .ToList();

        return new DiretorioPublicoDto
        {
            Estabelecimento = cartoes.FirstOrDefault(c => c.Anfitriao)?.Nome,
            Anunciantes = cartoes,
            GeradoEm = agora,
        };
    }

    /// <summary>
    /// Um botão por destino: o WhatsApp do perfil e o do QR de um anúncio
    /// (o mesmo número, com mensagem pronta) viram um só — fica o com mensagem,
    /// na posição do primeiro. Cada contato ganha o detalhe legível.
    /// </summary>
    public static List<ContatoDiretorioDto> SemRepetir(IEnumerable<ContatoDiretorioDto> contatos)
    {
        var resultado = new List<ContatoDiretorioDto>();
        var posicoes = new Dictionary<string, int>();

        foreach (var contato in contatos)
        {
            var (chave, detalhe) = Destino(contato);
            var comDetalhe = contato with { Detalhe = detalhe };

            if (!posicoes.TryGetValue(chave, out var i))
            {
                posicoes[chave] = resultado.Count;
                resultado.Add(comDetalhe);
            }
            else if (contato.Valor.Contains("?text=", StringComparison.Ordinal) && !resultado[i].Valor.Contains("?text=", StringComparison.Ordinal))
            {
                resultado[i] = comDetalhe;
            }
        }

        return resultado;
    }

    /// <summary>A identidade do destino (para não repetir) e o texto legível dele.</summary>
    private static (string Chave, string? Detalhe) Destino(ContatoDiretorioDto contato)
    {
        var uri = Uri.TryCreate(contato.Valor, UriKind.Absolute, out var u) ? u : null;

        switch (contato.Tipo)
        {
            case TipoQrCode.WhatsApp when uri is not null:
                var numero = uri.AbsolutePath.Trim('/');
                return ($"whatsapp:{numero}", TelefoneLegivel(numero));
            case TipoQrCode.Telefone:
                var digitos = new string(contato.Valor.Where(char.IsAsciiDigit).ToArray());
                return ($"telefone:{digitos}", TelefoneLegivel(digitos));
            case TipoQrCode.Instagram when uri is not null:
                var usuario = uri.AbsolutePath.Trim('/');
                return ($"instagram:{usuario.ToLowerInvariant()}", "@" + usuario);
            case TipoQrCode.Link when uri is not null:
                var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
                var caminho = uri.AbsolutePath.TrimEnd('/');
                return ($"link:{host.ToLowerInvariant()}{caminho}{uri.Query}", host + (caminho.Length > 0 ? caminho : string.Empty));
            default:
                return ($"{contato.Tipo}:{contato.Valor}", null);
        }
    }

    /// <summary>"5592999990000" → "(92) 99999-0000"; outro formato volta como veio.</summary>
    public static string TelefoneLegivel(string digitos)
    {
        // Os números ficam gravados com o país (55 + DDD + número).
        if (!digitos.StartsWith("55", StringComparison.Ordinal) || digitos.Length is not (12 or 13))
        {
            return "+" + digitos;
        }

        var local = digitos[2..];
        return $"({local[..2]}) {local[2..^4]}-{local[^4..]}";
    }

    public static string Rotulo(TipoQrCode tipo) => tipo switch
    {
        TipoQrCode.WhatsApp => "WhatsApp",
        TipoQrCode.Instagram => "Instagram",
        TipoQrCode.Telefone => "Ligar",
        TipoQrCode.Pix => "Copiar Pix",
        _ => "Site",
    };

    /// <summary>Maiúsculas e sem espaços; nulo se não tem o formato (nem vai ao banco).</summary>
    public static string? NormalizarCodigo(string? codigo)
    {
        var texto = codigo?.Trim().ToUpperInvariant();
        return texto is not null && FormatoCodigo().IsMatch(texto) ? texto : null;
    }

    [GeneratedRegex("^[A-Z0-9]{10}$")]
    private static partial Regex FormatoCodigo();
}
