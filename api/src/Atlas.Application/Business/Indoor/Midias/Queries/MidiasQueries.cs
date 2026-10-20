using Atlas.Application.Business.Veiculacao;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Common.Extensions.Pagination;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Midias.Queries;

[Authorize(Policy = BusinessPermissionKeys.Midias.View)]
public record GetMidiasQuery : IRequest<PaginatedList<MidiaDto>>
{
    public PaginationParams PaginationParams { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? Ativo { get; init; }
    public TipoMidia? Tipo { get; init; }
}

public class GetMidiasQueryHandler(
    IApplicationDbContext context, IClienteAtual clienteAtual, IIdentityService identityService, IConfiguracaoIndoor configuracao)
    : IRequestHandler<GetMidiasQuery, PaginatedList<MidiaDto>>
{
    public async Task<PaginatedList<MidiaDto>> Handle(GetMidiasQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        var query = context.Midias.AsNoTracking().Where(m => m.ClienteId == cliente.ClienteId);

        if (request.Ativo is { } ativo)
        {
            query = query.Where(m => m.Ativo == ativo);
        }

        if (request.Tipo is { } tipo)
        {
            query = query.Where(m => m.Tipo == tipo);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var termo = request.SearchTerm.Trim().ToLower();
            query = query.Where(m => m.Titulo.ToLower().Contains(termo));
        }

        var pagina = await query
            .OrderByDescending(m => m.Created)
            .ThenByDescending(m => m.Id)
            .ParaDto(context)
            .ToEntityPaginatedListAsync(context, "midia", request.PaginationParams, cancellationToken);

        await identityService.PreencherNomesAsync(pagina.Items, cancellationToken);

        foreach (var item in pagina.Items)
        {
            item.QrUrlRastreada = LinkQr.Montar(configuracao, item.CodigoQr);
        }

        return pagina;
    }
}

/// <summary>Detalhe com o endereço temporário para pré-visualizar o arquivo.</summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.View)]
public record GetMidiaByIdQuery(long Id) : IRequest<MidiaDto>;

public class GetMidiaByIdQueryHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    IIdentityService identityService,
    IArmazenamentoMidias armazenamento,
    IConfiguracaoIndoor configuracao)
    : IRequestHandler<GetMidiaByIdQuery, MidiaDto>
{
    public static readonly TimeSpan ValidadeVisualizacao = TimeSpan.FromHours(1);

    public async Task<MidiaDto> Handle(GetMidiaByIdQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);

        var midia = await context.Midias
            .AsNoTracking()
            .Where(m => m.Id == request.Id && m.ClienteId == cliente.ClienteId)
            .Select(m => new { m.ChaveArmazenamento })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, midia);

        var dto = await context.Midias
            .AsNoTracking()
            .Where(m => m.Id == request.Id)
            .ParaDto(context)
            .FirstAsync(cancellationToken);

        await identityService.PreencherNomesAsync([dto], cancellationToken);

        return dto with
        {
            UrlVisualizacao = await armazenamento.GerarUrlLeituraAsync(midia.ChaveArmazenamento, ValidadeVisualizacao, cancellationToken),
            QrUrlRastreada = LinkQr.Montar(configuracao, dto.CodigoQr),
        };
    }
}

/// <summary>Espaço, cota e plano (mídias ativas) do cliente, para o painel avisar antes do envio.</summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.View)]
public record GetUsoArmazenamentoQuery : IRequest<UsoArmazenamentoDto>;

public class GetUsoArmazenamentoQueryHandler(IApplicationDbContext context, IClienteAtual clienteAtual)
    : IRequestHandler<GetUsoArmazenamentoQuery, UsoArmazenamentoDto>
{
    public async Task<UsoArmazenamentoDto> Handle(GetUsoArmazenamentoQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        var (usado, cota) = await CotaArmazenamento.ObterAsync(context, cliente.ClienteId, cancellationToken);
        var (ativas, limite) = await LimiteMidiasAtivas.ObterAsync(context, cliente.ClienteId, cancellationToken);

        return new UsoArmazenamentoDto(usado, cota, Midia.TamanhoMaximoBytes, ativas, limite);
    }
}
