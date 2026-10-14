using Atlas.Application.Business.Indoor.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;

/// <summary>
/// A operadora cadastra a tela (o hardware é dela) e escolhe o parceiro que
/// vai gerir o conteúdo. A TV é pareada depois, com o código gerado aqui.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Create)]
public record CriarTelaCommand : IRequest<long>, IDadosTela
{
    public long ParceiroId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? Localizacao { get; init; }
    public string? IdentificacaoAparelho { get; init; }
}

/// <summary>
/// Dados da tela. Trocar o parceiro tira a playlist (era do parceiro
/// anterior), desvincula a TV e descarta o código pendente.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record AtualizarTelaCommand : IRequest<uint>, IDadosTela
{
    public long Id { get; init; }
    public long ParceiroId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? Localizacao { get; init; }
    public string? IdentificacaoAparelho { get; init; }
    public uint Version { get; init; }
}

public interface IDadosTela
{
    long ParceiroId { get; }
    string Nome { get; }
    string? Localizacao { get; }
    string? IdentificacaoAparelho { get; }
}

public class CriarTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CriarTelaCommand, long>
{
    public async Task<long> Handle(CriarTelaCommand request, CancellationToken cancellationToken)
    {
        var nome = request.Nome.Trim();

        await RegrasTela.GarantirParceiroAsync(context, request.ParceiroId, cancellationToken);
        await RegrasTela.GarantirNomeLivreAsync(context, request.ParceiroId, nome, null, cancellationToken);

        var tela = new Tela
        {
            ParceiroId = request.ParceiroId,
            Nome = nome,
            Localizacao = RegistrosDoParceiro.Limpar(request.Localizacao),
            IdentificacaoAparelho = RegistrosDoParceiro.Limpar(request.IdentificacaoAparelho),
        };

        context.Telas.Add(tela);
        await context.SaveChangesAsync(cancellationToken);

        return tela.Id;
    }
}

public class AtualizarTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AtualizarTelaCommand, uint>
{
    public async Task<uint> Handle(AtualizarTelaCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);
        var nome = request.Nome.Trim();

        if (request.ParceiroId != tela.ParceiroId)
        {
            await RegrasTela.GarantirParceiroAsync(context, request.ParceiroId, cancellationToken);
        }

        await RegrasTela.GarantirNomeLivreAsync(context, request.ParceiroId, nome, tela.Id, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.TransferirPara(request.ParceiroId);
        tela.Nome = nome;
        tela.Localizacao = RegistrosDoParceiro.Limpar(request.Localizacao);
        tela.IdentificacaoAparelho = RegistrosDoParceiro.Limpar(request.IdentificacaoAparelho);

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

/// <summary>Regras das telas na visão da operadora (sem filtro de parceiro).</summary>
internal static class RegrasTela
{
    /// <summary>A tela, rastreada; inexistente dá 404.</summary>
    public static async Task<Tela> CarregarAsync(IApplicationDbContext context, long id, CancellationToken cancellationToken)
    {
        var tela = await context.Telas.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        Guard.Against.NotFound(id, tela);

        return tela;
    }

    public static async Task GarantirParceiroAsync(IApplicationDbContext context, long parceiroId, CancellationToken cancellationToken)
    {
        if (!await context.Parceiros.AnyAsync(p => p.Id == parceiroId, cancellationToken))
        {
            throw new ValidationException(nameof(Tela.ParceiroId), ["Parceiro não encontrado."]);
        }
    }

    /// <summary>O nome é único dentro do parceiro (é como ele reconhece a TV).</summary>
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
            throw new ValidationException(nameof(Tela.Nome), ["Este parceiro já tem uma tela com este nome."]);
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
        validador.RuleFor(x => x.ParceiroId)
            .GreaterThan(0).WithMessage("Escolha o parceiro da tela.");

        validador.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da tela é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        validador.RuleFor(x => x.Localizacao)
            .MaximumLength(150).WithMessage("A localização deve ter no máximo 150 caracteres.");

        validador.RuleFor(x => x.IdentificacaoAparelho)
            .MaximumLength(100).WithMessage("A identificação do aparelho deve ter no máximo 100 caracteres.");
    }
}
