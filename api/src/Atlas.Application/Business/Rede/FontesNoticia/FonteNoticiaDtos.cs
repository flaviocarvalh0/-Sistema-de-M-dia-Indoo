using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Conteudo.Entities;

namespace Atlas.Application.Business.Rede.FontesNoticia;

public sealed record FonteNoticiaDto : IDtoAuditavel
{
    public long Id { get; init; }
    public string Nome { get; init; } = null!;
    public string UrlFeed { get; init; } = null!;
    public string Credito { get; init; } = null!;
    public bool Ativo { get; init; }
    public DateTimeOffset? UltimaColetaEm { get; init; }

    /// <summary>Erro da última leitura do feed; nulo quando deu certo.</summary>
    public string? UltimoErro { get; init; }

    /// <summary>Notícias guardadas (no máximo <see cref="FonteNoticia.NoticiasGuardadas"/>).</summary>
    public int QuantidadeNoticias { get; init; }
    public DateTimeOffset? NoticiaMaisRecenteEm { get; init; }

    public DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; set; }
    public uint Version { get; init; }
}

/// <summary>Notícia guardada, para a operadora conferir o que vai às TVs.</summary>
public sealed record NoticiaDto
{
    public long Id { get; init; }
    public string Titulo { get; init; } = null!;
    public DateTimeOffset PublicadaEm { get; init; }
    public DateTimeOffset ColetadaEm { get; init; }

    /// <summary>Endereço temporário (1 h) da imagem guardada; nulo sem imagem.</summary>
    public Uri? UrlImagem { get; init; }

    /// <summary>Ainda vai para as TVs (mais nova que 48 h).</summary>
    public bool Vigente { get; init; }
}

public static class FonteNoticiaProjecao
{
    public static IQueryable<FonteNoticiaDto> ParaDto(this IQueryable<FonteNoticia> query, IApplicationDbContext context) =>
        query.Select(f => new FonteNoticiaDto
        {
            Id = f.Id,
            Nome = f.Nome,
            UrlFeed = f.UrlFeed,
            Credito = f.Credito,
            Ativo = f.Ativo,
            UltimaColetaEm = f.UltimaColetaEm,
            UltimoErro = f.UltimoErro,
            QuantidadeNoticias = context.Noticias.Count(n => n.FonteNoticiaId == f.Id),
            NoticiaMaisRecenteEm = context.Noticias.Where(n => n.FonteNoticiaId == f.Id).Max(n => (DateTimeOffset?)n.PublicadaEm),
            Created = f.Created,
            CreatedBy = f.CreatedBy,
            LastModified = f.LastModified,
            LastModifiedBy = f.LastModifiedBy,
            Version = EF.Property<uint>(f, "Version"),
        });
}
