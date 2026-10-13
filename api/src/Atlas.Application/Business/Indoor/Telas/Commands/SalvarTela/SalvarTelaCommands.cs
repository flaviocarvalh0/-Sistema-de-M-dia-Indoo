using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Telas.Commands.SalvarTela;

/// <summary>Cadastra a tela; a TV é pareada depois, com o código gerado no painel.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Create)]
public record CriarTelaCommand : IRequest<long>, IDadosTela
{
    public string Nome { get; init; } = string.Empty;
    public string? Localizacao { get; init; }
    public long? PlaylistId { get; init; }
}

/// <summary>Nome, localização e a playlist que a TV exibe (vale na próxima sincronização).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record AtualizarTelaCommand : IRequest<uint>, IDadosTela
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? Localizacao { get; init; }
    public long? PlaylistId { get; init; }
    public uint Version { get; init; }
}

public interface IDadosTela
{
    string Nome { get; }
    string? Localizacao { get; }
    long? PlaylistId { get; }
}

public class CriarTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<CriarTelaCommand, long>
{
    public async Task<long> Handle(CriarTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var nome = request.Nome.Trim();

        await RegrasTela.GarantirNomeLivreAsync(context, parceiro.ParceiroId, nome, null, cancellationToken);
        await RegrasTela.GarantirPlaylistDoParceiroAsync(context, parceiro.ParceiroId, request.PlaylistId, cancellationToken);

        var tela = new Tela
        {
            ParceiroId = parceiro.ParceiroId,
            Nome = nome,
            Localizacao = RegistrosDoParceiro.Limpar(request.Localizacao),
            PlaylistId = request.PlaylistId,
        };

        context.Telas.Add(tela);
        await context.SaveChangesAsync(cancellationToken);

        return tela.Id;
    }
}

public class AtualizarTelaCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<AtualizarTelaCommand, uint>
{
    public async Task<uint> Handle(AtualizarTelaCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var tela = await RegistrosDoParceiro.CarregarAsync(context.Telas, parceiro.ParceiroId, request.Id, cancellationToken);
        var nome = request.Nome.Trim();

        await RegrasTela.GarantirNomeLivreAsync(context, parceiro.ParceiroId, nome, tela.Id, cancellationToken);
        await RegrasTela.GarantirPlaylistDoParceiroAsync(context, parceiro.ParceiroId, request.PlaylistId, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.Nome = nome;
        tela.Localizacao = RegistrosDoParceiro.Limpar(request.Localizacao);
        tela.PlaylistId = request.PlaylistId;

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

internal static class RegrasTela
{
    public static async Task GarantirNomeLivreAsync(
        IApplicationDbContext context,
        long parceiroId,
        string nome,
        long? idAtual,
        CancellationToken cancellationToken)
    {
        var normalizado = nome.ToLower();

        if (await context.Telas.AnyAsync(
                t => t.ParceiroId == parceiroId && t.Nome.ToLower() == normalizado && t.Id != idAtual,
                cancellationToken))
        {
            throw new ValidationException(nameof(Tela.Nome), ["Já existe uma tela com este nome."]);
        }
    }

    /// <summary>O banco não cruza o parceiro na FK: a playlist de outro parceiro "não existe".</summary>
    public static async Task GarantirPlaylistDoParceiroAsync(
        IApplicationDbContext context,
        long parceiroId,
        long? playlistId,
        CancellationToken cancellationToken)
    {
        if (playlistId is { } id
            && !await context.Playlists.AnyAsync(p => p.Id == id && p.ParceiroId == parceiroId, cancellationToken))
        {
            throw new ValidationException(nameof(Tela.PlaylistId), ["Playlist não encontrada."]);
        }
    }
}

public class CriarTelaCommandValidator : AbstractValidator<CriarTelaCommand>
{
    public CriarTelaCommandValidator() => this.RegrasDados();
}

public class AtualizarTelaCommandValidator : AbstractValidator<AtualizarTelaCommand>
{
    public AtualizarTelaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da tela é obrigatório.");
        this.RegrasDados();
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

internal static class ValidacaoTela
{
    public static void RegrasDados<T>(this AbstractValidator<T> validador) where T : IDadosTela
    {
        validador.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da tela é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        validador.RuleFor(x => x.Localizacao)
            .MaximumLength(150).WithMessage("A localização deve ter no máximo 150 caracteres.");

        validador.RuleFor(x => x.PlaylistId)
            .GreaterThan(0).WithMessage("Playlist inválida.")
            .When(x => x.PlaylistId is not null);
    }
}
