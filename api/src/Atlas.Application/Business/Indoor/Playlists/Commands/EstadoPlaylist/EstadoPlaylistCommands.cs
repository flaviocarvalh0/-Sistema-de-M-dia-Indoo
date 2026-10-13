using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Playlists.Commands.EstadoPlaylist;

[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record AtivarPlaylistCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Inativa: as telas que a exibem passam a mostrar a tela de espera.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record InativarPlaylistCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>Apaga a playlist e os itens (as mídias ficam). Só se nenhuma tela a exibe.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Delete)]
public record ExcluirPlaylistCommand(long Id) : IRequest;

public class AtivarPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtivarPlaylistCommand, uint>
{
    public async Task<uint> Handle(AtivarPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var playlist = await RegistrosDoParceiro.CarregarAsync(context.Playlists, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(playlist, request.Version);
        playlist.Ativo = true;

        return await context.SalvarAsync(playlist, cancellationToken);
    }
}

public class InativarPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<InativarPlaylistCommand, uint>
{
    public async Task<uint> Handle(InativarPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var playlist = await RegistrosDoParceiro.CarregarAsync(context.Playlists, parceiro.ParceiroId, request.Id, cancellationToken);

        context.ConferirVersao(playlist, request.Version);
        playlist.Ativo = false;

        return await context.SalvarAsync(playlist, cancellationToken);
    }
}

public class ExcluirPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<ExcluirPlaylistCommand>
{
    public async Task Handle(ExcluirPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var playlist = await RegistrosDoParceiro.CarregarAsync(
            context.Playlists.Include(p => p.Itens), parceiro.ParceiroId, request.Id, cancellationToken);

        var telas = await context.Telas.CountAsync(t => t.PlaylistId == playlist.Id, cancellationToken);
        if (telas > 0)
        {
            throw new ValidationException(nameof(Playlist.Id),
                [$"A playlist está em {telas} tela(s). Troque a playlist dessas telas antes de excluir."]);
        }

        context.Playlists.Remove(playlist);
        await context.SaveChangesAsync(cancellationToken);
    }
}
