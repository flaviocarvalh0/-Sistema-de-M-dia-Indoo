using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Playlists.Dtos;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Playlists.Queries;

/// <summary>As playlists da rede (operadora).</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.View)]
public record GetPlaylistsQuery : IRequest<PaginatedList<PlaylistDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? Ativo { get; init; }
}

public class GetPlaylistsQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetPlaylistsQuery, PaginatedList<PlaylistDto>>
{
    public async Task<PaginatedList<PlaylistDto>> Handle(GetPlaylistsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Playlists.AsNoTracking();

        if (request.Ativo is { } ativo)
        {
            query = query.Where(p => p.Ativo == ativo);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(p => p.Nome.ToLower().Contains(termo));
        }

        var pagina = await query
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "playlist", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

/// <summary>Playlist com os itens na ordem do loop.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.View)]
public record GetPlaylistByIdQuery(long Id) : IRequest<PlaylistDto>;

public class GetPlaylistByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetPlaylistByIdQuery, PlaylistDto>
{
    public async Task<PlaylistDto> Handle(GetPlaylistByIdQuery request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .ParaDto(context)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, playlist);

        var itens = await context.ItensPlaylist
            .AsNoTracking()
            .Where(i => i.PlaylistId == playlist.Id)
            .ParaDto()
            .ToListAsync(cancellationToken);

        await identityService.PreencherNomesAsync([playlist], cancellationToken);

        return playlist with { Itens = itens };
    }
}
