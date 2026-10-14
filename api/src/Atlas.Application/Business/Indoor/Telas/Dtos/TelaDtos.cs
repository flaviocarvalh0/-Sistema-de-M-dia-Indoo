using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Telas.Dtos;

/// <summary>
/// A tela como a operadora (Rede → Telas) e o parceiro (Mídia Indoor → Telas)
/// a veem. Para o parceiro, o código de pareamento vem sempre vazio: parear é
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

    /// <summary>Código ainda válido para parear a TV (some ao parear ou ao expirar).</summary>
    public string? CodigoPareamento { get; init; }

    public DateTimeOffset? CodigoPareamentoExpiraEm { get; init; }

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
                : t.Estado != null && t.Estado.UltimaSincronizacao >= limiteOnline
                    ? SituacaoTela.Online
                    : SituacaoTela.Offline,
            PareadaEm = t.PareadaEm,
            UltimaSincronizacao = t.Estado != null ? t.Estado.UltimaSincronizacao : null,
            VersaoApp = t.Estado != null ? t.Estado.VersaoApp : null,
            CodigoPareamento = comPareamento && t.CodigoPareamentoExpiraEm > agora ? t.CodigoPareamento : null,
            CodigoPareamentoExpiraEm = comPareamento && t.CodigoPareamentoExpiraEm > agora ? t.CodigoPareamentoExpiraEm : null,
            Created = t.Created,
            CreatedBy = t.CreatedBy,
            LastModified = t.LastModified,
            LastModifiedBy = t.LastModifiedBy,
            Version = EF.Property<uint>(t, "Version"),
        });
    }
}
