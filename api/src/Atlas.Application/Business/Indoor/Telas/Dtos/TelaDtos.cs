using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Telas.Dtos;

/// <summary>
/// A tela como a operadora (Rede → Telas) e o cliente (Onde meus anúncios
/// passam) a veem. O parceiro é o local onde a TV está. Para o cliente, o código de pareamento vem sempre vazio: parear é
/// trabalho da operadora.
/// </summary>
public sealed record TelaDto : IDtoAuditavel
{
    public long Id { get; init; }
    public long ParceiroId { get; init; }
    public string ParceiroNome { get; init; } = null!;
    public string Nome { get; init; } = null!;
    public string? Localizacao { get; init; }

    /// <summary>MAC, número de série ou etiqueta anotados na instalação (informativo).</summary>
    public string? IdentificacaoAparelho { get; init; }

    public long? PlaylistId { get; init; }
    public string? PlaylistNome { get; init; }
    public bool Ativo { get; init; }

    /// <summary>Aguardando pareamento, Online ou Offline (pela última sincronização).</summary>
    public SituacaoTela Situacao { get; init; }

    public DateTimeOffset? PareadaEm { get; init; }
    public DateTimeOffset? UltimaSincronizacao { get; init; }
    public string? VersaoApp { get; init; }

    /// <summary>O <c>versionCode</c> do app na TV; nulo em app sem atualização automática.</summary>
    public int? VersaoCodigoApp { get; init; }

    /// <summary>Por que está Fora de exibição (só com essa situação).</summary>
    public MotivoForaDeExibicao? MotivoForaDeExibicao { get; init; }

    /// <summary>Desde quando a TV sincroniza sem exibir (antes de virar Fora de exibição, aos 10 min).</summary>
    public DateTimeOffset? ForaDeExibicaoDesde { get; init; }

    /// <summary>Relógio da TV menos o do servidor (s); fora de ±5 min o filtro do dia pode errar.</summary>
    public int? DesvioRelogioSegundos { get; init; }

    /// <summary>Recebe as versões do app em teste.</summary>
    public bool TvDeTeste { get; init; }

    /// <summary>Código ainda válido para parear a TV (some ao parear ou ao expirar).</summary>
    public string? CodigoPareamento { get; init; }

    public DateTimeOffset? CodigoPareamentoExpiraEm { get; init; }

    /// <summary>A TV mostra o QR code do diretório de anunciantes.</summary>
    public bool DiretorioAtivo { get; init; }

    /// <summary>Código do endereço público (só na visão da operadora).</summary>
    public string? CodigoDiretorio { get; init; }

    /// <summary>
    /// Endereço público do diretório (só na visão da operadora); nulo sem
    /// código ou sem <c>Indoor:UrlPublica</c> configurada na API.
    /// </summary>
    public Uri? UrlDiretorio { get; set; }

    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

/// <summary>Código que a pessoa digita na TV.</summary>
/// <param name="Codigo">8 caracteres, sem os que se confundem (0/O, 1/I).</param>
/// <param name="ExpiraEm">Depois disso é preciso gerar outro.</param>
/// <param name="Version">Nova versão da tela.</param>
public sealed record CodigoPareamentoDto(string Codigo, DateTimeOffset ExpiraEm, uint Version);

/// <summary>Endereço novo do diretório.</summary>
/// <param name="Codigo">10 caracteres.</param>
/// <param name="Url">Endereço completo; nulo sem <c>Indoor:UrlPublica</c>.</param>
/// <param name="Version">Nova versão da tela.</param>
public sealed record CodigoDiretorioDto(string Codigo, Uri? Url, uint Version);

public static class TelaProjecao
{
    /// <summary>
    /// A situação e o código ainda válido dependem do instante: calculados no
    /// SQL com <paramref name="agora"/> (mesma regra de <see cref="Tela.CalcularSituacao"/>).
    /// </summary>
    /// <param name="query">Telas já filtradas.</param>
    /// <param name="agora">Instante da consulta.</param>
    /// <param name="comPareamento">Traz o código pendente (só na visão da operadora).</param>
    public static IQueryable<TelaDto> ParaDto(this IQueryable<Tela> query, DateTimeOffset agora, bool comPareamento)
    {
        var limiteOnline = agora - Tela.ToleranciaOffline;
        var limiteForaDeExibicao = agora - Tela.ToleranciaForaDeExibicao;

        return query.Select(t => new TelaDto
        {
            Id = t.Id,
            ParceiroId = t.ParceiroId,
            ParceiroNome = t.Parceiro.Nome,
            Nome = t.Nome,
            Localizacao = t.Localizacao,
            IdentificacaoAparelho = t.IdentificacaoAparelho,
            PlaylistId = t.PlaylistId,
            PlaylistNome = t.Playlist != null ? t.Playlist.Nome : null,
            Ativo = t.Ativo,
            Situacao = t.DispositivoId == null
                ? SituacaoTela.AguardandoPareamento
                : t.Estado == null || t.Estado.UltimaSincronizacao < limiteOnline
                    ? SituacaoTela.Offline
                    : t.Estado.ForaDeExibicaoDesde != null && t.Estado.ForaDeExibicaoDesde <= limiteForaDeExibicao
                        ? SituacaoTela.ForaDeExibicao
                        : SituacaoTela.Online,
            MotivoForaDeExibicao = t.Estado != null && t.Estado.ForaDeExibicaoDesde != null && t.Estado.ForaDeExibicaoDesde <= limiteForaDeExibicao
                ? t.Estado.MotivoForaDeExibicao
                : null,
            ForaDeExibicaoDesde = t.Estado != null ? t.Estado.ForaDeExibicaoDesde : null,
            DesvioRelogioSegundos = t.Estado != null ? t.Estado.DesvioRelogioSegundos : null,
            PareadaEm = t.PareadaEm,
            UltimaSincronizacao = t.Estado != null ? t.Estado.UltimaSincronizacao : null,
            VersaoApp = t.Estado != null ? t.Estado.VersaoApp : null,
            VersaoCodigoApp = t.Estado != null ? t.Estado.VersaoCodigoApp : null,
            TvDeTeste = t.TvDeTeste,
            CodigoPareamento = comPareamento && t.CodigoPareamentoExpiraEm > agora ? t.CodigoPareamento : null,
            CodigoPareamentoExpiraEm = comPareamento && t.CodigoPareamentoExpiraEm > agora ? t.CodigoPareamentoExpiraEm : null,
            DiretorioAtivo = t.DiretorioAtivo,
            CodigoDiretorio = comPareamento ? t.CodigoDiretorio : null,
            Created = t.Created,
            CreatedBy = t.CreatedBy,
            LastModified = t.LastModified,
            LastModifiedBy = t.LastModifiedBy,
            Version = EF.Property<uint>(t, "Version"),
        });
    }
}
