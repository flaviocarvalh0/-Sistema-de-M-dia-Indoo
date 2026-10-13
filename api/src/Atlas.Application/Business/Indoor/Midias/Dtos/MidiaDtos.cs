using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Midias.Dtos;

public sealed record MidiaDto : IDtoAuditavel
{
    public long Id { get; init; }
    public string Titulo { get; init; } = null!;
    public TipoMidia Tipo { get; init; }
    public string TipoConteudo { get; init; } = null!;
    public long TamanhoBytes { get; init; }
    public string Hash { get; init; } = null!;
    public int DuracaoSegundos { get; init; }
    public DateOnly? InicioExibicao { get; init; }
    public DateOnly? FimExibicao { get; init; }
    public bool Ativo { get; init; }

    /// <summary>Em quantas playlists a mídia aparece (mídia em uso não pode ser excluída).</summary>
    public int QuantidadePlaylists { get; init; }

    /// <summary>Endereço temporário para pré-visualizar no painel. Só no detalhe (<c>GET /{id}</c>).</summary>
    public Uri? UrlVisualizacao { get; init; }

    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

/// <summary>Onde e até quando o navegador pode enviar o arquivo.</summary>
/// <param name="Chave">Volta no <c>CriarMidiaCommand</c> depois do envio.</param>
/// <param name="UrlEnvio">Destino do <c>PUT</c> com o arquivo no corpo e o <c>Content-Type</c> do arquivo.</param>
/// <param name="ExpiraEm">Até quando a URL aceita o envio (2 h no Supabase).</param>
public sealed record EnvioMidiaDto(string Chave, Uri UrlEnvio, DateTimeOffset ExpiraEm);

/// <summary>Uso do storage pelo parceiro, para o painel mostrar antes do envio.</summary>
public sealed record UsoArmazenamentoDto(long UsadoBytes, long CotaBytes, long TamanhoMaximoArquivoBytes);

public static class MidiaProjecao
{
    public static IQueryable<MidiaDto> ParaDto(this IQueryable<Midia> query, IApplicationDbContext context) =>
        query.Select(m => new MidiaDto
        {
            Id = m.Id,
            Titulo = m.Titulo,
            Tipo = m.Tipo,
            TipoConteudo = m.TipoConteudo,
            TamanhoBytes = m.TamanhoBytes,
            Hash = m.Hash,
            DuracaoSegundos = m.DuracaoSegundos,
            InicioExibicao = m.InicioExibicao,
            FimExibicao = m.FimExibicao,
            Ativo = m.Ativo,
            QuantidadePlaylists = context.ItensPlaylist.Where(i => i.MidiaId == m.Id).Select(i => i.PlaylistId).Distinct().Count(),
            Created = m.Created,
            CreatedBy = m.CreatedBy,
            LastModified = m.LastModified,
            LastModifiedBy = m.LastModifiedBy,
            Version = EF.Property<uint>(m, "Version"),
        });
}
