using Atlas.Application.Business.Clientes.Common;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Veiculacao;

// Relatório de veiculação (ADR-023): o que a TV registrou que exibiu e quantas
// pessoas leram o QR, por cliente e período. É a prova que o anunciante recebe.

public sealed record RelatorioVeiculacaoDto
{
    public long ClienteId { get; init; }
    public string ClienteNome { get; init; } = null!;

    /// <summary>Período, inclusive, no fuso do cliente.</summary>
    public DateOnly De { get; init; }
    public DateOnly Ate { get; init; }
    public DateTimeOffset GeradoEm { get; init; }

    /// <summary>Exibições registradas pelas TVs (concluídas + interrompidas).</summary>
    public int Exibicoes { get; init; }

    /// <summary>Foram até o fim (vídeo terminou ou imagem cumpriu o tempo).</summary>
    public int Concluidas { get; init; }

    /// <summary>Saíram antes por erro ao abrir ou tocar o arquivo.</summary>
    public int Interrompidas { get; init; }

    /// <summary>Exibidas com a TV sem falar com a API (tocou do que já tinha).</summary>
    public int SemConexao { get; init; }

    /// <summary>Tempo somado na tela.</summary>
    public long SegundosNaTela { get; init; }

    /// <summary>Leituras do QR rastreado e leitores únicos (por dia).</summary>
    public int LeiturasQr { get; init; }
    public int LeitoresUnicosQr { get; init; }

    /// <summary>
    /// Falso por enquanto: o registro diz que o app exibiu, não que a TV estava
    /// ligada e na entrada certa (detecção de tela ligada fica para depois).
    /// </summary>
    public bool MedeTelaLigada { get; init; }

    public IReadOnlyList<VeiculacaoPorTelaDto> PorTela { get; init; } = [];
    public IReadOnlyList<VeiculacaoPorMidiaDto> PorMidia { get; init; } = [];
    public IReadOnlyList<VeiculacaoPorDiaDto> PorDia { get; init; } = [];

    /// <summary>24 posições (0 a 23 h), na hora local de cada TV.</summary>
    public IReadOnlyList<int> PorHora { get; init; } = [];
}

/// <summary>Exibições numa TV.</summary>
/// <param name="TelaId">A tela.</param>
/// <param name="Nome">Nome da tela; "(TV removida)" se a tela foi excluída depois.</param>
/// <param name="Parceiro">O local onde a TV está.</param>
/// <param name="Exibicoes">Quantas vezes passou.</param>
/// <param name="SegundosNaTela">Tempo somado na tela.</param>
public sealed record VeiculacaoPorTelaDto(long TelaId, string Nome, string? Parceiro, int Exibicoes, long SegundosNaTela);

/// <summary>Exibições e leituras do QR de uma mídia.</summary>
/// <param name="MidiaId">A mídia.</param>
/// <param name="Titulo">"(mídia excluída)" se a mídia foi excluída depois.</param>
/// <param name="Exibicoes">Quantas vezes passou.</param>
/// <param name="LeiturasQr">Leituras do QR rastreado.</param>
/// <param name="LeitoresUnicosQr">Leitores únicos por dia, somados.</param>
/// <param name="QrRastreado">A mídia tem hoje um QR rastreado (link, WhatsApp ou Instagram).</param>
public sealed record VeiculacaoPorMidiaDto(long MidiaId, string Titulo, int Exibicoes, int LeiturasQr, int LeitoresUnicosQr, bool QrRastreado);

public sealed record VeiculacaoPorDiaDto(DateOnly Data, int Exibicoes, int LeiturasQr);

/// <summary>Relatório do próprio cliente (painel do anunciante). Sem datas: do dia 1 do mês até hoje.</summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.View)]
public sealed record GetMeuRelatorioVeiculacaoQuery(DateOnly? De = null, DateOnly? Ate = null) : IRequest<RelatorioVeiculacaoDto>;

/// <summary>Relatório de um cliente na visão da operadora.</summary>
[Authorize(Policy = BusinessPermissionKeys.Clientes.View)]
public sealed record GetRelatorioVeiculacaoClienteQuery(long ClienteId, DateOnly? De = null, DateOnly? Ate = null) : IRequest<RelatorioVeiculacaoDto>;

public class GetMeuRelatorioVeiculacaoQueryHandler(IApplicationDbContext context, IClienteAtual clienteAtual, TimeProvider timeProvider)
    : IRequestHandler<GetMeuRelatorioVeiculacaoQuery, RelatorioVeiculacaoDto>
{
    public async Task<RelatorioVeiculacaoDto> Handle(GetMeuRelatorioVeiculacaoQuery request, CancellationToken cancellationToken)
    {
        var cliente = await clienteAtual.ObterAsync(cancellationToken);
        return await RelatorioVeiculacao.MontarAsync(context, cliente.ClienteId, request.De, request.Ate, timeProvider.GetUtcNow(), cancellationToken);
    }
}

public class GetRelatorioVeiculacaoClienteQueryHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetRelatorioVeiculacaoClienteQuery, RelatorioVeiculacaoDto>
{
    public Task<RelatorioVeiculacaoDto> Handle(GetRelatorioVeiculacaoClienteQuery request, CancellationToken cancellationToken) =>
        RelatorioVeiculacao.MontarAsync(context, request.ClienteId, request.De, request.Ate, timeProvider.GetUtcNow(), cancellationToken);
}

public class GetMeuRelatorioVeiculacaoQueryValidator : AbstractValidator<GetMeuRelatorioVeiculacaoQuery>
{
    public GetMeuRelatorioVeiculacaoQueryValidator() => RelatorioVeiculacao.ValidarPeriodo(this, x => x.De, x => x.Ate);
}

public class GetRelatorioVeiculacaoClienteQueryValidator : AbstractValidator<GetRelatorioVeiculacaoClienteQuery>
{
    public GetRelatorioVeiculacaoClienteQueryValidator() => RelatorioVeiculacao.ValidarPeriodo(this, x => x.De, x => x.Ate);
}

public static class RelatorioVeiculacao
{
    /// <summary>Período máximo de um relatório (um trimestre).</summary>
    public const int DiasMaximos = 93;

    public static void ValidarPeriodo<T>(AbstractValidator<T> validador, Func<T, DateOnly?> de, Func<T, DateOnly?> ate)
    {
        validador.RuleFor(x => x)
            .Must(x => de(x) is not { } d || ate(x) is not { } a || d <= a)
            .WithName("De")
            .WithMessage("A data inicial deve ser anterior ou igual à final.");

        validador.RuleFor(x => x)
            .Must(x => de(x) is not { } d || ate(x) is not { } a || a.DayNumber - d.DayNumber < DiasMaximos)
            .WithName("Ate")
            .WithMessage($"O período deve ter no máximo {DiasMaximos} dias.");
    }

    public static async Task<RelatorioVeiculacaoDto> MontarAsync(
        IApplicationDbContext context,
        long clienteId,
        DateOnly? de,
        DateOnly? ate,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        var cliente = await context.Clientes
            .AsNoTracking()
            .Where(c => c.Id == clienteId)
            .Select(c => new { c.Id, c.Nome, c.FusoHorario })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(clienteId, cliente);

        var fusoCliente = TimeZoneInfo.FindSystemTimeZoneById(cliente.FusoHorario);
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora, fusoCliente).DateTime);
        var fim = ate ?? hoje;
        var inicio = de ?? new DateOnly(fim.Year, fim.Month, 1);
        if (inicio > fim)
        {
            inicio = fim;
        }

        var inicioUtc = InicioDoDia(inicio, fusoCliente);
        var fimUtc = InicioDoDia(fim.AddDays(1), fusoCliente);

        var exibicoes = await context.RegistrosExibicao
            .AsNoTracking()
            .Where(r => r.ClienteId == clienteId && r.Inicio >= inicioUtc && r.Inicio < fimUtc)
            .Select(r => new { r.TelaId, r.MidiaId, r.Inicio, r.DuracaoMs, r.Concluida, r.SemConexao })
            .ToListAsync(cancellationToken);

        var leituras = await context.LeiturasQr
            .AsNoTracking()
            .Where(l => l.ClienteId == clienteId && l.Momento >= inicioUtc && l.Momento < fimUtc)
            .Select(l => new { l.MidiaId, l.Momento, l.Visitante })
            .ToListAsync(cancellationToken);

        var telaIds = exibicoes.Select(e => e.TelaId).Distinct().ToList();
        var telas = await context.Telas
            .AsNoTracking()
            .Where(t => telaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Nome, Parceiro = t.Parceiro.Nome, t.Parceiro.FusoHorario })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var midiaIds = exibicoes.Select(e => e.MidiaId).Concat(leituras.Select(l => l.MidiaId)).Distinct().ToList();
        var midias = await context.Midias
            .AsNoTracking()
            .Where(m => midiaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Titulo, Rastreado = m.CodigoQr != null })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        // Hora e dia de cada exibição no fuso do local da TV (é o horário que o
        // anunciante reconhece: "passou às 7 h na academia").
        var fusos = new Dictionary<string, TimeZoneInfo>();
        TimeZoneInfo FusoDaTela(long telaId) =>
            telas.TryGetValue(telaId, out var t) ? Fuso(fusos, t.FusoHorario) : fusoCliente;

        var porHora = new int[24];
        var porDia = new Dictionary<DateOnly, (int Exibicoes, int Leituras)>();
        foreach (var e in exibicoes)
        {
            var local = TimeZoneInfo.ConvertTime(e.Inicio, FusoDaTela(e.TelaId));
            porHora[local.Hour]++;
            var dia = DateOnly.FromDateTime(local.DateTime);
            porDia[dia] = porDia.TryGetValue(dia, out var v) ? (v.Exibicoes + 1, v.Leituras) : (1, 0);
        }

        foreach (var l in leituras)
        {
            var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(l.Momento, fusoCliente).DateTime);
            porDia[dia] = porDia.TryGetValue(dia, out var v) ? (v.Exibicoes, v.Leituras + 1) : (0, 1);
        }

        // O hash do visitante já muda a cada dia: distintos = leitores únicos por dia, somados.
        static int Unicos<TLeitura>(IEnumerable<TLeitura> lista, Func<TLeitura, string> visitante) =>
            lista.Select(visitante).Distinct().Count();

        return new RelatorioVeiculacaoDto
        {
            ClienteId = cliente.Id,
            ClienteNome = cliente.Nome,
            De = inicio,
            Ate = fim,
            GeradoEm = agora,
            Exibicoes = exibicoes.Count,
            Concluidas = exibicoes.Count(e => e.Concluida),
            Interrompidas = exibicoes.Count(e => !e.Concluida),
            SemConexao = exibicoes.Count(e => e.SemConexao),
            SegundosNaTela = exibicoes.Sum(e => (long)e.DuracaoMs) / 1000,
            LeiturasQr = leituras.Count,
            LeitoresUnicosQr = Unicos(leituras, l => l.Visitante),
            MedeTelaLigada = false,
            PorTela = exibicoes
                .GroupBy(e => e.TelaId)
                .Select(g => new VeiculacaoPorTelaDto(
                    g.Key,
                    telas.TryGetValue(g.Key, out var t) ? t.Nome : "(TV removida)",
                    telas.TryGetValue(g.Key, out var t2) ? t2.Parceiro : null,
                    g.Count(),
                    g.Sum(e => (long)e.DuracaoMs) / 1000))
                .OrderByDescending(t => t.Exibicoes)
                .ToList(),
            PorMidia = midiaIds
                .Select(id =>
                {
                    var lidas = leituras.Where(l => l.MidiaId == id).ToList();
                    return new VeiculacaoPorMidiaDto(
                        id,
                        midias.TryGetValue(id, out var m) ? m.Titulo : "(mídia excluída)",
                        exibicoes.Count(e => e.MidiaId == id),
                        lidas.Count,
                        Unicos(lidas, l => l.Visitante),
                        midias.TryGetValue(id, out var m2) && m2.Rastreado);
                })
                .OrderByDescending(m => m.Exibicoes)
                .ToList(),
            PorDia = porDia
                .OrderBy(d => d.Key)
                .Select(d => new VeiculacaoPorDiaDto(d.Key, d.Value.Exibicoes, d.Value.Leituras))
                .ToList(),
            PorHora = porHora,
        };
    }

    private static DateTimeOffset InicioDoDia(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo Fuso(Dictionary<string, TimeZoneInfo> cache, string id)
    {
        if (!cache.TryGetValue(id, out var fuso))
        {
            fuso = TimeZoneInfo.FindSystemTimeZoneById(id);
            cache[id] = fuso;
        }

        return fuso;
    }
}
