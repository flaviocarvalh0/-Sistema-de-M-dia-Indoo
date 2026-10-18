using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Indoor.Midias.Queries;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Rede.Midias.Queries;

/// <summary>
/// As mídias de todos os clientes, para a operadora montar as grades.
/// Somente leitura: o criativo é do anunciante.
/// </summary>
/// <remarks>
/// A operadora lê as mídias para montar as grades: exige
/// <c>Playlists.View</c> e <c>Clientes.View</c>, que só ela tem. Não usa
/// <c>Midias.View</c> de propósito: essa permissão mostraria a ela o módulo
/// do cliente (Mídia Indoor), cujas telas dependem de um cliente logado.
/// </remarks>
[Authorize(Policy = BusinessPermissionKeys.Playlists.View)]
[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public record GetMidiasRedeQuery : IRequest<PaginatedList<MidiaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public long? ClienteId { get; init; }
    public bool? Ativo { get; init; }
    public TipoMidia? Tipo { get; init; }

    /// <summary>Só as que têm troca de arquivo aguardando aprovação (mais antigas primeiro).</summary>
    public bool? TrocaPendente { get; init; }
}

public class GetMidiasRedeQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetMidiasRedeQuery, PaginatedList<MidiaDto>>
{
    public async Task<PaginatedList<MidiaDto>> Handle(GetMidiasRedeQuery request, CancellationToken cancellationToken)
    {
        var query = context.Midias.AsNoTracking();

        if (request.ClienteId is { } clienteId)
        {
            query = query.Where(m => m.ClienteId == clienteId);
        }

        if (request.Ativo is { } ativo)
        {
            query = query.Where(m => m.Ativo == ativo);
        }

        if (request.Tipo is { } tipo)
        {
            query = query.Where(m => m.Tipo == tipo);
        }

        if (request.TrocaPendente is { } pendente)
        {
            query = query.Where(m => (m.PendenteChave != null) == pendente);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(m => m.Titulo.ToLower().Contains(termo) || m.Cliente.Nome.ToLower().Contains(termo));
        }

        var ordenada = request.TrocaPendente == true
            ? query.OrderBy(m => m.PendenteEnviadoEm)
            : query.OrderBy(m => m.Cliente.Nome);

        var pagina = await ordenada
            .ThenBy(m => m.Titulo)
            .ThenBy(m => m.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "midia", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        return pagina;
    }
}

/// <summary>Detalhe com o endereço temporário para a operadora pré-visualizar.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.View)]
[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public record GetMidiaRedeByIdQuery(long Id) : IRequest<MidiaDto>;

public class GetMidiaRedeByIdQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IArmazenamentoMidias armazenamento)
    : IRequestHandler<GetMidiaRedeByIdQuery, MidiaDto>
{
    public async Task<MidiaDto> Handle(GetMidiaRedeByIdQuery request, CancellationToken cancellationToken)
    {
        var chaves = await context.Midias
            .AsNoTracking()
            .Where(m => m.Id == request.Id)
            .Select(m => new { m.ChaveArmazenamento, m.PendenteChave })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, chaves);

        var dto = await context.Midias
            .AsNoTracking()
            .Where(m => m.Id == request.Id)
            .ParaDto(context)
            .FirstAsync(cancellationToken);

        await identityService.PreencherNomesAsync([dto], cancellationToken);

        return dto with
        {
            UrlVisualizacao = await armazenamento.GerarUrlLeituraAsync(chaves.ChaveArmazenamento, GetMidiaByIdQueryHandler.ValidadeVisualizacao, cancellationToken),
            UrlVisualizacaoPendente = chaves.PendenteChave is { } pendente
                ? await armazenamento.GerarUrlLeituraAsync(pendente, GetMidiaByIdQueryHandler.ValidadeVisualizacao, cancellationToken)
                : null,
        };
    }
}
