using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Dispositivo.Exibicoes;

/// <summary>Uma exibição como a TV registrou.</summary>
/// <param name="Id">UUID gerado na TV; reenviar o mesmo id não duplica.</param>
/// <param name="MidiaId">A mídia que apareceu.</param>
/// <param name="Inicio">Quando apareceu (relógio da TV).</param>
/// <param name="DuracaoMs">Quanto tempo ficou na tela.</param>
/// <param name="Concluida">Foi até o fim (falso = interrompida por erro).</param>
/// <param name="SemConexao">A TV estava sem falar com a API quando exibiu.</param>
public sealed record ExibicaoInformada(Guid Id, long MidiaId, DateTimeOffset Inicio, int DuracaoMs, bool Concluida, bool SemConexao);

/// <param name="Aceitas">Gravadas agora.</param>
/// <param name="JaRecebidas">Já estavam gravadas (reenvio).</param>
/// <param name="Descartadas">Mídia desconhecida, data fora da janela ou duração inválida.</param>
public sealed record RegistroExibicoesDto(int Aceitas, int JaRecebidas, int Descartadas);

/// <summary>
/// Recebe um lote de exibições da TV (prova de veiculação, ADR-023). A TV
/// apaga o lote do banco local quando recebe 200, inclusive as descartadas:
/// reenviar não as tornaria válidas.
/// </summary>
/// <remarks>Autenticado pelo token da TV (rota do dispositivo), não por permissão de usuário.</remarks>
public sealed record RegistrarExibicoesCommand(IReadOnlyList<ExibicaoInformada> Exibicoes) : IRequest<RegistroExibicoesDto>;

public class RegistrarExibicoesCommandValidator : AbstractValidator<RegistrarExibicoesCommand>
{
    public const int TamanhoMaximoLote = 500;

    public RegistrarExibicoesCommandValidator()
    {
        RuleFor(x => x.Exibicoes)
            .NotNull().WithMessage("Envie a lista de exibições.")
            .Must(l => l is null || l.Count <= TamanhoMaximoLote)
            .WithMessage($"Envie no máximo {TamanhoMaximoLote} exibições por lote.");
    }
}

public class RegistrarExibicoesCommandHandler(
    IApplicationDbContext context,
    IDispositivoAtual dispositivoAtual,
    TimeProvider timeProvider)
    : IRequestHandler<RegistrarExibicoesCommand, RegistroExibicoesDto>
{
    /// <summary>Mais velho que isto não entra (a TV guardou demais ou o relógio dela está errado).</summary>
    public static readonly TimeSpan JanelaPassado = TimeSpan.FromDays(35);

    /// <summary>Tolerância para relógio adiantado.</summary>
    public static readonly TimeSpan JanelaFuturo = TimeSpan.FromMinutes(10);

    public async Task<RegistroExibicoesDto> Handle(RegistrarExibicoesCommand request, CancellationToken cancellationToken)
    {
        var dispositivo = await dispositivoAtual.ObterAsync(cancellationToken);
        var agora = timeProvider.GetUtcNow();

        if (request.Exibicoes.Count == 0)
        {
            return new RegistroExibicoesDto(0, 0, 0);
        }

        // O parceiro de agora (a tela pode ter mudado de local desde o pareamento).
        var parceiroId = await context.Telas
            .Where(t => t.Id == dispositivo.TelaId)
            .Select(t => t.ParceiroId)
            .FirstAsync(cancellationToken);

        var midiaIds = request.Exibicoes.Select(e => e.MidiaId).Distinct().ToList();
        var donos = await context.Midias
            .Where(m => midiaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.ClienteId })
            .ToDictionaryAsync(m => m.Id, m => m.ClienteId, cancellationToken);

        var ids = request.Exibicoes.Select(e => e.Id).Distinct().ToList();
        var jaGravados = (await context.RegistrosExibicao
                .Where(r => r.TelaId == dispositivo.TelaId && ids.Contains(r.IdLocal))
                .Select(r => r.IdLocal)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var vistos = new HashSet<Guid>();
        int aceitas = 0, repetidas = 0, descartadas = 0;

        foreach (var exibicao in request.Exibicoes)
        {
            if (jaGravados.Contains(exibicao.Id) || !vistos.Add(exibicao.Id))
            {
                repetidas++;
                continue;
            }

            if (exibicao.Id == Guid.Empty
                || !donos.TryGetValue(exibicao.MidiaId, out var clienteId)
                || exibicao.Inicio < agora - JanelaPassado
                || exibicao.Inicio > agora + JanelaFuturo
                || exibicao.DuracaoMs < 0
                || exibicao.DuracaoMs > RegistroExibicao.DuracaoMaxima.TotalMilliseconds)
            {
                descartadas++;
                continue;
            }

            context.RegistrosExibicao.Add(new RegistroExibicao
            {
                TelaId = dispositivo.TelaId,
                ParceiroId = parceiroId,
                MidiaId = exibicao.MidiaId,
                ClienteId = clienteId,
                IdLocal = exibicao.Id,
                Inicio = exibicao.Inicio.ToUniversalTime(),
                DuracaoMs = exibicao.DuracaoMs,
                Concluida = exibicao.Concluida,
                SemConexao = exibicao.SemConexao,
                RecebidoEm = agora,
            });
            aceitas++;
        }

        if (aceitas > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new RegistroExibicoesDto(aceitas, repetidas, descartadas);
    }
}
