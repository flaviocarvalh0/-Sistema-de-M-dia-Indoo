using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.FontesNoticia;

/// <summary>Os feeds de notícias da rede.</summary>
[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.View)]
public record GetFontesNoticiaQuery : IRequest<PaginatedList<FonteNoticiaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? Ativo { get; init; }
}

public class GetFontesNoticiaQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetFontesNoticiaQuery, PaginatedList<FonteNoticiaDto>>
{
    public async Task<PaginatedList<FonteNoticiaDto>> Handle(GetFontesNoticiaQuery request, CancellationToken cancellationToken)
    {
        var query = context.FontesNoticia.AsNoTracking();

        if (request.Ativo is { } ativo)
        {
            query = query.Where(f => f.Ativo == ativo);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(f => f.Nome.ToLower().Contains(termo) || f.UrlFeed.ToLower().Contains(termo));
        }

        var pagina = await query
            .OrderBy(f => f.Nome)
            .ThenBy(f => f.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "fonte_noticia", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.View)]
public record GetFonteNoticiaByIdQuery(long Id) : IRequest<FonteNoticiaDto>;

public class GetFonteNoticiaByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetFonteNoticiaByIdQuery, FonteNoticiaDto>
{
    public async Task<FonteNoticiaDto> Handle(GetFonteNoticiaByIdQuery request, CancellationToken cancellationToken)
    {
        var fonte = await context.FontesNoticia
            .AsNoTracking()
            .Where(f => f.Id == request.Id)
            .ParaDto(context)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, fonte);

        await identityService.PreencherNomesAsync([fonte], cancellationToken);

        return fonte;
    }
}

/// <summary>As notícias guardadas de uma fonte, mais recentes primeiro, com a imagem para conferir.</summary>
[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.View)]
public record GetNoticiasDaFonteQuery(long Id) : IRequest<IReadOnlyList<NoticiaDto>>;

public class GetNoticiasDaFonteQueryHandler(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    TimeProvider timeProvider)
    : IRequestHandler<GetNoticiasDaFonteQuery, IReadOnlyList<NoticiaDto>>
{
    public async Task<IReadOnlyList<NoticiaDto>> Handle(GetNoticiasDaFonteQuery request, CancellationToken cancellationToken)
    {
        var fonteId = await context.FontesNoticia
            .Where(f => f.Id == request.Id)
            .Select(f => (long?)f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, fonteId);

        var noticias = await context.Noticias
            .AsNoTracking()
            .Where(n => n.FonteNoticiaId == request.Id)
            .OrderByDescending(n => n.PublicadaEm)
            .ThenByDescending(n => n.Id)
            .Select(n => new { n.Id, n.Titulo, n.PublicadaEm, n.ColetadaEm, n.ChaveImagem })
            .ToListAsync(cancellationToken);

        var chaves = noticias.Where(n => n.ChaveImagem is not null).Select(n => n.ChaveImagem!).Distinct().ToList();
        IReadOnlyDictionary<string, Uri> urls = new Dictionary<string, Uri>();
        if (chaves.Count > 0)
        {
            try
            {
                urls = await armazenamento.GerarUrlsLeituraAsync(chaves, TimeSpan.FromHours(1), cancellationToken);
            }
            catch (ArmazenamentoIndisponivelException)
            {
                // A lista vale sem as imagens.
            }
        }

        var limite = timeProvider.GetUtcNow() - Noticia.Validade;

        return noticias.Select(n => new NoticiaDto
        {
            Id = n.Id,
            Titulo = n.Titulo,
            PublicadaEm = n.PublicadaEm,
            ColetadaEm = n.ColetadaEm,
            UrlImagem = n.ChaveImagem is { } chave && urls.TryGetValue(chave, out var url) ? url : null,
            Vigente = n.PublicadaEm >= limite,
        }).ToList();
    }
}
