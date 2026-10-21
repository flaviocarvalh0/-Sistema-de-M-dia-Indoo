using System.Globalization;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Diretorio;

/// <summary>Quanto o diretório de uma TV foi usado nos últimos dias (ADR-027), para a operadora.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
public sealed record GetUsoDiretorioTelaQuery(long TelaId, int Dias = 30) : IRequest<UsoDiretorioDto>;

/// <param name="Dias">Janela contada, até agora.</param>
/// <param name="Acessos">Vezes que a página foi aberta (QR lido).</param>
/// <param name="Pessoas">Pessoas diferentes por dia, somadas.</param>
/// <param name="ToquesContatos">Toques nos contatos dos anunciantes.</param>
/// <param name="ToquesAnuncie">Toques em "Quer anunciar aqui?".</param>
/// <param name="PorAnunciante">Toques por anunciante, do mais tocado.</param>
public sealed record UsoDiretorioDto(
    int Dias,
    int Acessos,
    int Pessoas,
    int ToquesContatos,
    int ToquesAnuncie,
    IReadOnlyList<ToquesAnuncianteDto> PorAnunciante);

public sealed record ToquesAnuncianteDto(long ClienteId, string Nome, int Toques);

public class GetUsoDiretorioTelaQueryHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetUsoDiretorioTelaQuery, UsoDiretorioDto>
{
    public const int DiasMaximos = 93;

    public async Task<UsoDiretorioDto> Handle(GetUsoDiretorioTelaQuery request, CancellationToken cancellationToken)
    {
        if (!await context.Telas.AnyAsync(t => t.Id == request.TelaId, cancellationToken))
        {
            throw new NotFoundException(request.TelaId.ToString(CultureInfo.InvariantCulture), "Tela");
        }

        var dias = Math.Clamp(request.Dias, 1, DiasMaximos);
        var desde = timeProvider.GetUtcNow().AddDays(-dias);

        var interacoes = await context.InteracoesDiretorio
            .AsNoTracking()
            .Where(i => i.TelaId == request.TelaId && i.Momento >= desde)
            .Select(i => new { i.Evento, i.ClienteId, i.Visitante })
            .ToListAsync(cancellationToken);

        var acessos = interacoes.Where(i => i.Evento == EventoDiretorio.Acesso).ToList();
        var porCliente = interacoes
            .Where(i => i.Evento == EventoDiretorio.Contato && i.ClienteId is not null)
            .GroupBy(i => i.ClienteId!.Value)
            .Select(g => (ClienteId: g.Key, Toques: g.Count()))
            .ToList();

        var ids = porCliente.Select(c => c.ClienteId).ToList();
        var nomes = await context.Clientes
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);

        return new UsoDiretorioDto(
            dias,
            acessos.Count,
            // O hash do visitante muda a cada dia: distintos = pessoas por dia, somadas.
            acessos.Select(a => a.Visitante).Distinct().Count(),
            porCliente.Sum(c => c.Toques),
            interacoes.Count(i => i.Evento == EventoDiretorio.Anuncie),
            porCliente
                .Select(c => new ToquesAnuncianteDto(c.ClienteId, nomes.GetValueOrDefault(c.ClienteId, "(cliente excluído)"), c.Toques))
                .OrderByDescending(c => c.Toques)
                .ThenBy(c => c.Nome)
                .ToList());
    }
}
