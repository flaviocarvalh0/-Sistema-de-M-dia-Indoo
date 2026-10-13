using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Parceiros.Dtos;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Rede.Parceiros.Queries;

[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetParceirosQuery : IRequest<PaginatedList<ParceiroDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public SituacaoParceiro? Situacao { get; init; }
}

public class GetParceirosQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetParceirosQuery, PaginatedList<ParceiroDto>>
{
    public async Task<PaginatedList<ParceiroDto>> Handle(GetParceirosQuery request, CancellationToken cancellationToken)
    {
        var query = context.Parceiros.AsNoTracking();

        if (request.Situacao is { } situacao)
        {
            query = query.Where(p => p.Situacao == situacao);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            var digitos = DocumentosBrasil.SomenteDigitos(termo);

            query = query.Where(p =>
                p.Nome.ToLower().Contains(termo)
                || (p.NomeResponsavel != null && p.NomeResponsavel.ToLower().Contains(termo))
                || (p.Cidade != null && p.Cidade.ToLower().Contains(termo))
                || p.Email.ToLower().Contains(termo)
                || (digitos.Length > 0 && p.Telefone != null && p.Telefone.Contains(digitos))
                || (digitos.Length > 0 && p.Documento != null && p.Documento.Contains(digitos)));
        }

        var pagina = await query
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "parceiro", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetParceiroByIdQuery(long Id) : IRequest<ParceiroDto>;

public class GetParceiroByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetParceiroByIdQuery, ParceiroDto>
{
    public async Task<ParceiroDto> Handle(GetParceiroByIdQuery request, CancellationToken cancellationToken)
    {
        var parceiro = await context.Parceiros
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .ParaDto(context)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, parceiro);

        await identityService.PreencherNomesAsync([parceiro], cancellationToken);

        return parceiro;
    }
}
