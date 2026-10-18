using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Telas.Queries;

/// <summary>
/// "Onde meus anúncios passam": as telas ativas cuja playlist (ativa) tem
/// alguma mídia ativa do cliente do usuário logado. Somente leitura.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
public record GetTelasQuery : IRequest<PaginatedList<TelaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class GetTelasQueryHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    IIdentityService identityService,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelasQuery, PaginatedList<TelaDto>>
{
    public async Task<PaginatedList<TelaDto>> Handle(GetTelasQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        var query = context.Telas.AsNoTracking().OndePassa(cliente.ClienteId);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(t => t.Nome.ToLower().Contains(termo)
                || t.Parceiro.Nome.ToLower().Contains(termo)
                || (t.Localizacao != null && t.Localizacao.ToLower().Contains(termo)));
        }

        var pagina = await query
            .OrderBy(t => t.Nome)
            .ThenBy(t => t.Id)
            .ParaDto(timeProvider.GetUtcNow(), comPareamento: false)
            .ToEntityPaginatedListAsync(context, "tela", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
public record GetTelaByIdQuery(long Id) : IRequest<TelaDto>;

public class GetTelaByIdQueryHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    IIdentityService identityService,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelaByIdQuery, TelaDto>
{
    public async Task<TelaDto> Handle(GetTelaByIdQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);

        var tela = await context.Telas
            .AsNoTracking()
            .OndePassa(cliente.ClienteId)
            .Where(t => t.Id == request.Id)
            .ParaDto(timeProvider.GetUtcNow(), comPareamento: false)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, tela);

        await identityService.PreencherNomesAsync([tela], cancellationToken);

        return tela;
    }
}

internal static class TelasDoAnunciante
{
    /// <summary>
    /// Telas que exibem o cliente agora: tela e playlist ativas, com algum
    /// item de mídia ativa dele. Mídia de anunciante bloqueado não toca, e um
    /// anunciante bloqueado nem chega aqui (o painel dele é barrado antes).
    /// </summary>
    public static IQueryable<Tela> OndePassa(this IQueryable<Tela> telas, long clienteId) =>
        telas.Where(t => t.Ativo
            && t.Playlist != null
            && t.Playlist.Ativo
            && t.Playlist.Itens.Any(i => i.Midia.ClienteId == clienteId && i.Midia.Ativo));
}

