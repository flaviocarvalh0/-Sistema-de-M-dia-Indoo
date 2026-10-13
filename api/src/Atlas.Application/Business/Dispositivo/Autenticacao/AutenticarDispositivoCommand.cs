using Atlas.Application.Business.Common;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Dispositivo.Autenticacao;

/// <summary>
/// A TV troca o código de pareamento (gerado no painel) por um token próprio.
/// Anônimo, com limite de tentativas por IP. Código inválido, expirado, de
/// tela inativa ou de parceiro bloqueado dão a mesma resposta: a TV não
/// descobre qual foi o motivo (nem se o código existe).
/// </summary>
public record AutenticarDispositivoCommand : IRequest<DispositivoAutenticadoDto>
{
    public string CodigoPareamento { get; init; } = string.Empty;

    /// <summary>Identificador gerado pelo app na instalação (ex.: UUID), guardado no aparelho.</summary>
    public string DispositivoId { get; init; } = string.Empty;

    public string? VersaoApp { get; init; }
}

public sealed record DispositivoAutenticadoDto(
    string Token,
    DateTimeOffset ExpiraEm,
    long TelaId,
    string NomeTela,
    string NomeParceiro,
    int IntervaloSincronizacaoSegundos);

public class AutenticarDispositivoCommandHandler(
    IApplicationDbContext context,
    IGeradorTokenDispositivo geradorToken,
    IConfiguracaoIndoor configuracao,
    TimeProvider timeProvider)
    : IRequestHandler<AutenticarDispositivoCommand, DispositivoAutenticadoDto>
{
    public const string MensagemCodigoInvalido = "Código inválido ou expirado. Gere um novo código no painel.";

    public async Task<DispositivoAutenticadoDto> Handle(AutenticarDispositivoCommand request, CancellationToken cancellationToken)
    {
        var codigo = NormalizarCodigo(request.CodigoPareamento);
        var agora = timeProvider.GetUtcNow();

        var tela = await context.Telas
            .Include(t => t.Parceiro)
            .FirstOrDefaultAsync(t => t.CodigoPareamento == codigo, cancellationToken);

        if (tela is null
            || !tela.CodigoPareamentoValido(codigo, agora)
            || tela.Parceiro.Situacao == SituacaoParceiro.Bloqueado)
        {
            throw new ValidationException(nameof(request.CodigoPareamento), [MensagemCodigoInvalido]);
        }

        tela.Parear(request.DispositivoId.Trim(), agora);

        tela.Estado ??= await context.EstadosTela.FirstOrDefaultAsync(e => e.TelaId == tela.Id, cancellationToken) ?? new EstadoTela();
        tela.Estado.UltimaSincronizacao = agora;
        tela.Estado.VersaoApp = Limpar(request.VersaoApp);

        await context.SaveChangesAsync(cancellationToken);

        var token = geradorToken.Gerar(tela.Id, tela.ParceiroId, tela.DispositivoId!, tela.VersaoCredencial);

        return new DispositivoAutenticadoDto(
            token.Token,
            token.ExpiraEm,
            tela.Id,
            tela.Nome,
            tela.Parceiro.Nome,
            (int)configuracao.IntervaloSincronizacao.TotalSeconds);
    }

    /// <summary>Maiúsculas, sem espaços nem hífens: "ab12-cd34" vale "AB12CD34".</summary>
    public static string NormalizarCodigo(string? codigo) =>
        new((codigo ?? string.Empty).Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public class AutenticarDispositivoCommandValidator : AbstractValidator<AutenticarDispositivoCommand>
{
    public AutenticarDispositivoCommandValidator()
    {
        RuleFor(x => x.CodigoPareamento)
            .Must(c => AutenticarDispositivoCommandHandler.NormalizarCodigo(c).Length == Tela.TamanhoCodigoPareamento)
            .WithMessage(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);

        RuleFor(x => x.DispositivoId)
            .NotEmpty().WithMessage("O identificador do aparelho é obrigatório.")
            .MaximumLength(100).WithMessage("O identificador do aparelho deve ter no máximo 100 caracteres.");

        RuleFor(x => x.VersaoApp)
            .MaximumLength(30).WithMessage("A versão do app deve ter no máximo 30 caracteres.");
    }
}
