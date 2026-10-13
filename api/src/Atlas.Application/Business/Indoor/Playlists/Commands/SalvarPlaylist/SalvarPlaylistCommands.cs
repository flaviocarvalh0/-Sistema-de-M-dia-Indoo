using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Playlists.Commands.SalvarPlaylist;

/// <summary>Cria a playlist vazia; os itens vêm em <c>DefinirItensPlaylistCommand</c>.</summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Create)]
public record CriarPlaylistCommand : IRequest<long>, IDadosPlaylist
{
    public string Nome { get; init; } = string.Empty;
    public string? Descricao { get; init; }
}

[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record AtualizarPlaylistCommand : IRequest<uint>, IDadosPlaylist
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? Descricao { get; init; }
    public uint Version { get; init; }
}

public interface IDadosPlaylist
{
    string Nome { get; }
    string? Descricao { get; }
}

public class CriarPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<CriarPlaylistCommand, long>
{
    public async Task<long> Handle(CriarPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var nome = request.Nome.Trim();

        await RegrasPlaylist.GarantirNomeLivreAsync(context, parceiro.ParceiroId, nome, null, cancellationToken);

        var playlist = new Playlist
        {
            ParceiroId = parceiro.ParceiroId,
            Nome = nome,
            Descricao = RegistrosDoParceiro.Limpar(request.Descricao),
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        return playlist.Id;
    }
}

public class AtualizarPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtualizarPlaylistCommand, uint>
{
    public async Task<uint> Handle(AtualizarPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var playlist = await RegistrosDoParceiro.CarregarAsync(context.Playlists, parceiro.ParceiroId, request.Id, cancellationToken);
        var nome = request.Nome.Trim();

        await RegrasPlaylist.GarantirNomeLivreAsync(context, parceiro.ParceiroId, nome, playlist.Id, cancellationToken);

        context.ConferirVersao(playlist, request.Version);
        playlist.Nome = nome;
        playlist.Descricao = RegistrosDoParceiro.Limpar(request.Descricao);

        return await context.SalvarAsync(playlist, cancellationToken);
    }
}

internal static class RegrasPlaylist
{
    public static async Task GarantirNomeLivreAsync(
        IApplicationDbContext context,
        long parceiroId,
        string nome,
        long? idAtual,
        CancellationToken cancellationToken)
    {
        var normalizado = nome.ToLower();

        if (await context.Playlists.AnyAsync(
                p => p.ParceiroId == parceiroId && p.Nome.ToLower() == normalizado && p.Id != idAtual,
                cancellationToken))
        {
            throw new ValidationException(nameof(Playlist.Nome), ["Já existe uma playlist com este nome."]);
        }
    }
}

public class CriarPlaylistCommandValidator : AbstractValidator<CriarPlaylistCommand>
{
    public CriarPlaylistCommandValidator() => this.RegrasDados();
}

public class AtualizarPlaylistCommandValidator : AbstractValidator<AtualizarPlaylistCommand>
{
    public AtualizarPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da playlist é obrigatório.");
        this.RegrasDados();
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

internal static class ValidacaoPlaylist
{
    public static void RegrasDados<T>(this AbstractValidator<T> validador) where T : IDadosPlaylist
    {
        validador.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da playlist é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        validador.RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("A descrição deve ter no máximo 300 caracteres.");
    }
}
