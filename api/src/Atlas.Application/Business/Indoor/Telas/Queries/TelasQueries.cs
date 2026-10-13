using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Indoor.Telas.Queries;

[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
public record GetTelasQuery : IRequest<PaginatedList<TelaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? Ativo { get; init; }
    public long? PlaylistId { get; init; }
}

public class GetTelasQueryHandler(
    IApplicationDbContext context,
    IParceiroAtual parceiroAtual,
    IIdentityService identityService,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelasQuery, PaginatedList<TelaDto>>
{
    public async Task<PaginatedList<TelaDto>> Handle(GetTelasQuery request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var query = context.Telas.AsNoTracking().Where(t => t.ParceiroId == parceiro.ParceiroId);

        if (request.Ativo is { } ativo)
        {
            query = query.Where(t => t.Ativo == ativo);
        }

        if (request.PlaylistId is { } playlistId)
        {
            query = query.Where(t => t.PlaylistId == playlistId);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(t => t.Nome.ToLower().Contains(termo)
                || (t.Localizacao != null && t.Localizacao.ToLower().Contains(termo)));
        }

        var pagina = await query
            .OrderBy(t => t.Nome)
            .ThenBy(t => t.Id)
            .ParaDto(timeProvider.GetUtcNow())
            .ToEntityPaginatedListAsync(context, "tela", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
public record GetTelaByIdQuery(long Id) : IRequest<TelaDto>;

public class GetTelaByIdQueryHandler(
    IApplicationDbContext context,
    IParceiroAtual parceiroAtual,
    IIdentityService identityService,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelaByIdQuery, TelaDto>
{
    public async Task<TelaDto> Handle(GetTelaByIdQuery request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);

        var tela = await context.Telas
            .AsNoTracking()
            .Where(t => t.Id == request.Id && t.ParceiroId == parceiro.ParceiroId)
            .ParaDto(timeProvider.GetUtcNow())
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, tela);

        await identityService.PreencherNomesAsync([tela], cancellationToken);

        return tela;
    }
}
