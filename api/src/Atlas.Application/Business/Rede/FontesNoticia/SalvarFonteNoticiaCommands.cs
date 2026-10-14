using Atlas.Application.Business.Indoor.Common;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Constants;

namespace Atlas.Application.Business.Rede.FontesNoticia;

/// <summary>Cadastra um feed RSS ou Atom; a primeira leitura acontece na próxima coleta (ou em "Coletar agora").</summary>
[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.Create)]
public record CriarFonteNoticiaCommand : IRequest<long>, IDadosFonteNoticia
{
    public string Nome { get; init; } = string.Empty;
    public string UrlFeed { get; init; } = string.Empty;
    public string Credito { get; init; } = string.Empty;
    public bool Ativo { get; init; } = true;
}

[Authorize(Policy = BusinessPermissionKeys.FonteNoticias.Update)]
public record AtualizarFonteNoticiaCommand : IRequest<uint>, IDadosFonteNoticia
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string UrlFeed { get; init; } = string.Empty;
    public string Credito { get; init; } = string.Empty;
    public bool Ativo { get; init; } = true;
    public uint Version { get; init; }
}

public interface IDadosFonteNoticia
{
    string Nome { get; }
    string UrlFeed { get; }
    string Credito { get; }
    bool Ativo { get; }
}

public class CriarFonteNoticiaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CriarFonteNoticiaCommand, long>
{
    public async Task<long> Handle(CriarFonteNoticiaCommand request, CancellationToken cancellationToken)
    {
        var fonte = new FonteNoticia();
        RegrasFonteNoticia.Aplicar(fonte, request);
        await RegrasFonteNoticia.GarantirUnicaAsync(context, fonte, cancellationToken);

        context.FontesNoticia.Add(fonte);
        await context.SaveChangesAsync(cancellationToken);

        return fonte.Id;
    }
}

public class AtualizarFonteNoticiaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AtualizarFonteNoticiaCommand, uint>
{
    public async Task<uint> Handle(AtualizarFonteNoticiaCommand request, CancellationToken cancellationToken)
    {
        var fonte = await RegrasFonteNoticia.CarregarAsync(context, request.Id, cancellationToken);

        context.ConferirVersao(fonte, request.Version);

        var trocouFeed = !string.Equals(fonte.UrlFeed, request.UrlFeed.Trim(), StringComparison.Ordinal);
        RegrasFonteNoticia.Aplicar(fonte, request);
        await RegrasFonteNoticia.GarantirUnicaAsync(context, fonte, cancellationToken);

        // Outro feed: o erro da leitura anterior não vale mais.
        if (trocouFeed)
        {
            fonte.UltimoErro = null;
        }

        return await context.SalvarAsync(fonte, cancellationToken);
    }
}

internal static class RegrasFonteNoticia
{
    public static async Task<FonteNoticia> CarregarAsync(IApplicationDbContext context, long id, CancellationToken cancellationToken)
    {
        var fonte = await context.FontesNoticia.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        Guard.Against.NotFound(id, fonte);

        return fonte;
    }

    public static void Aplicar(FonteNoticia fonte, IDadosFonteNoticia dados)
    {
        fonte.Nome = dados.Nome.Trim();
        fonte.UrlFeed = dados.UrlFeed.Trim();
        fonte.Credito = dados.Credito.Trim();
        fonte.Ativo = dados.Ativo;
    }

    public static async Task GarantirUnicaAsync(IApplicationDbContext context, FonteNoticia fonte, CancellationToken cancellationToken)
    {
        var nome = fonte.Nome.ToLower();
        if (await context.FontesNoticia.AnyAsync(f => f.Id != fonte.Id && f.Nome.ToLower() == nome, cancellationToken))
        {
            throw new ValidationException(nameof(FonteNoticia.Nome), ["Já existe uma fonte com este nome."]);
        }

        if (await context.FontesNoticia.AnyAsync(f => f.Id != fonte.Id && f.UrlFeed == fonte.UrlFeed, cancellationToken))
        {
            throw new ValidationException(nameof(FonteNoticia.UrlFeed), ["Este feed já está cadastrado."]);
        }
    }
}

public class CriarFonteNoticiaCommandValidator : AbstractValidator<CriarFonteNoticiaCommand>
{
    public CriarFonteNoticiaCommandValidator() => this.RegrasDados();
}

public class AtualizarFonteNoticiaCommandValidator : AbstractValidator<AtualizarFonteNoticiaCommand>
{
    public AtualizarFonteNoticiaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da fonte é obrigatório.");
        this.RegrasDados();
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

internal static class ValidacaoFonteNoticia
{
    public static void RegrasDados<T>(this AbstractValidator<T> validador) where T : IDadosFonteNoticia
    {
        validador.RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da fonte é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        validador.RuleFor(x => x.UrlFeed)
            .NotEmpty().WithMessage("O endereço do feed é obrigatório.")
            .MaximumLength(500).WithMessage("O endereço deve ter no máximo 500 caracteres.")
            .Must(EnderecoValido).WithMessage("Informe o endereço completo do feed RSS ou Atom (https://…).");

        validador.RuleFor(x => x.Credito)
            .NotEmpty().WithMessage("O crédito exibido na TV é obrigatório (ex.: \"Fonte: Agência Brasil\").")
            .MaximumLength(100).WithMessage("O crédito deve ter no máximo 100 caracteres.");
    }

    private static bool EnderecoValido(string url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
