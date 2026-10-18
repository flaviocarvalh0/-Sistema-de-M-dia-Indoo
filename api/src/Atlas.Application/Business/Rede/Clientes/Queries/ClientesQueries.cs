using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Clientes.Dtos;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Application.Business.Rede.Clientes.Queries;

[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public record GetClientesQuery : IRequest<PaginatedList<ClienteDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public SituacaoCliente? Situacao { get; init; }
}

public class GetClientesQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetClientesQuery, PaginatedList<ClienteDto>>
{
    public async Task<PaginatedList<ClienteDto>> Handle(GetClientesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Clientes.AsNoTracking();

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
            .ToEntityPaginatedListAsync(context, "cliente", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public record GetClienteByIdQuery(long Id) : IRequest<ClienteDto>;

public class GetClienteByIdQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetClienteByIdQuery, ClienteDto>
{
    public async Task<ClienteDto> Handle(GetClienteByIdQuery request, CancellationToken cancellationToken)
    {
        var cliente = await context.Clientes
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .ParaDto(context)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, cliente);

        await identityService.PreencherNomesAsync([cliente], cancellationToken);

        return cliente;
    }
}
