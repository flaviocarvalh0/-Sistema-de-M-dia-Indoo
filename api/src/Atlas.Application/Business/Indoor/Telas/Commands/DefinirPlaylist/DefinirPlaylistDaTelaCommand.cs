using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Telas.Commands.DefinirPlaylist;

/// <summary>
/// O que o parceiro pode mudar numa tela: a playlist que ela exibe (vale na
/// próxima sincronização da TV). Nula = a TV mostra a tela de espera.
/// </summary>
/// <remarks>
/// É gestão de conteúdo, não de hardware: exige ver a tela e alterar
/// playlists, e não <c>Telas.Update</c> (que é da operadora).
/// </remarks>
[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record DefinirPlaylistDaTelaCommand(long Id, long? PlaylistId, uint Version) : IRequest<uint>;

public class DefinirPlaylistDaTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<DefinirPlaylistDaTelaCommand, uint>
{
    public async Task<uint> Handle(DefinirPlaylistDaTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var tela = await RegistrosDoParceiro.CarregarAsync(context.Telas, parceiro.ParceiroId, request.Id, cancellationToken);

        // O banco não cruza o parceiro na FK: a playlist de outro parceiro "não existe".
        if (request.PlaylistId is { } playlistId
            && !await context.Playlists.AnyAsync(p => p.Id == playlistId && p.ParceiroId == parceiro.ParceiroId, cancellationToken))
        {
            throw new ValidationException(nameof(Tela.PlaylistId), ["Playlist não encontrada."]);
        }

        context.ConferirVersao(tela, request.Version);
        tela.PlaylistId = request.PlaylistId;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

public class DefinirPlaylistDaTelaCommandValidator : AbstractValidator<DefinirPlaylistDaTelaCommand>
{
    public DefinirPlaylistDaTelaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da tela é obrigatório.");
        RuleFor(x => x.PlaylistId).GreaterThan(0).WithMessage("Playlist inválida.").When(x => x.PlaylistId is not null);
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
