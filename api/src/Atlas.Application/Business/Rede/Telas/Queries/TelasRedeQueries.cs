using Atlas.Application.Business.Common;
using Atlas.Application.Business.Diretorio;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.Telas.Queries;

/// <summary>
/// Todas as telas da rede, como a operadora as vê (com o código de
/// pareamento pendente).
/// </summary>
/// <remarks>
/// O parceiro também tem <c>Telas.View</c> (vê as próprias TVs): a exigência
/// de <c>Parceiros.View</c>, que só a operadora tem, impede que ele liste as
/// telas dos outros parceiros por aqui.
/// </remarks>
[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetTelasRedeQuery : IRequest<PaginatedList<TelaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public long? ParceiroId { get; init; }
    public bool? Ativo { get; init; }
}

public class GetTelasRedeQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelasRedeQuery, PaginatedList<TelaDto>>
{
    public async Task<PaginatedList<TelaDto>> Handle(GetTelasRedeQuery request, CancellationToken cancellationToken)
    {
        var query = context.Telas.AsNoTracking();

        if (request.ParceiroId is { } parceiroId)
        {
            query = query.Where(t => t.ParceiroId == parceiroId);
        }

        if (request.Ativo is { } ativo)
        {
            query = query.Where(t => t.Ativo == ativo);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(t => t.Nome.ToLower().Contains(termo)
                || t.Parceiro.Nome.ToLower().Contains(termo)
                || (t.Localizacao != null && t.Localizacao.ToLower().Contains(termo))
                || (t.IdentificacaoAparelho != null && t.IdentificacaoAparelho.ToLower().Contains(termo)));
        }

        var pagina = await query
            .OrderBy(t => t.Parceiro.Nome)
            .ThenBy(t => t.Nome)
            .ThenBy(t => t.Id)
            .ParaDto(timeProvider.GetUtcNow(), comPareamento: true)
            .ToEntityPaginatedListAsync(context, "tela", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);
        foreach (var tela in pagina.Items)
        {
            tela.UrlDiretorio = EnderecoDiretorio.Montar(configuracao, tela.CodigoDiretorio);
        }

        return pagina;
    }
}

[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public record GetTelaRedeByIdQuery(long Id) : IRequest<TelaDto>;

public class GetTelaRedeByIdQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider)
    : IRequestHandler<GetTelaRedeByIdQuery, TelaDto>
{
    public async Task<TelaDto> Handle(GetTelaRedeByIdQuery request, CancellationToken cancellationToken)
    {
        var tela = await context.Telas
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .ParaDto(timeProvider.GetUtcNow(), comPareamento: true)
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, tela);

        await identityService.PreencherNomesAsync([tela], cancellationToken);
        tela.UrlDiretorio = EnderecoDiretorio.Montar(configuracao, tela.CodigoDiretorio);

        return tela;
    }
}
