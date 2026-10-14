using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Indoor.Midias.Commands.EstadoMidia;

/// <summary>
/// Volta a mídia às grades em que ela está. Respeita o plano do parceiro
/// (<c>LimiteMidiasAtivas</c>): passar do limite dá 400.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record AtivarMidiaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Tira a mídia de todas as grades sem perder o lugar dela nas playlists.</summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public record InativarMidiaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>
/// Apaga a mídia e o arquivo, liberando a cota. Só fora de playlists: em uso,
/// tire das playlists antes (ou inative).
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Delete)]
public record ExcluirMidiaCommand(long Id) : IRequest;

public class AtivarMidiaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtivarMidiaCommand, uint>
{
    public async Task<uint> Handle(AtivarMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoParceiro.CarregarAsync(context.Midias, parceiro.ParceiroId, request.Id, cancellationToken);

        if (!midia.Ativo)
        {
            await LimiteMidiasAtivas.GarantirVagaAsync(context, parceiro.ParceiroId, cancellationToken);
        }

        context.ConferirVersao(midia, request.Version);
        midia.Ativo = true;

        return await context.SalvarAsync(midia, cancellationToken);
    }
}

public class InativarMidiaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<InativarMidiaCommand, uint>
{
    public async Task<uint> Handle(InativarMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoParceiro.CarregarAsync(context.Midias, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(midia, request.Version);
        midia.Ativo = false;

        return await context.SalvarAsync(midia, cancellationToken);
    }
}

public class ExcluirMidiaCommandHandler(
    IApplicationDbContext context,
    IParceiroAtual parceiroAtual,
    IArmazenamentoMidias armazenamento,
    ILogger<ExcluirMidiaCommandHandler> logger)
    : IRequestHandler<ExcluirMidiaCommand>
{
    public async Task Handle(ExcluirMidiaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var midia = await RegistrosDoParceiro.CarregarAsync(context.Midias, parceiro.ParceiroId, request.Id, cancellationToken);

        var playlists = await context.ItensPlaylist
            .Where(i => i.MidiaId == midia.Id)
            .Select(i => i.PlaylistId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (playlists > 0)
        {
            throw new ValidationException(nameof(Midia.Id),
                [$"A mídia está em {playlists} playlist(s). Tire-a das playlists antes de excluir, ou apenas inative."]);
        }

        context.Midias.Remove(midia);
        await context.SaveChangesAsync(cancellationToken);

        // Depois do banco: se o storage falhar, sobra um arquivo órfão (só
        // espaço), nunca uma mídia apontando para arquivo que não existe.
        try
        {
            await armazenamento.RemoverAsync(midia.ChaveArmazenamento, cancellationToken);
        }
        catch (ArmazenamentoIndisponivelException ex)
        {
            logger.LogWarning(ex, "Mídia {Id} excluída, mas o arquivo {Chave} ficou no storage.", midia.Id, midia.ChaveArmazenamento);
        }
    }
}
