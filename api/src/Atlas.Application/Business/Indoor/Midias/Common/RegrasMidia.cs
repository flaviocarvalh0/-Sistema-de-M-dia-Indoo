using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>Regras de campo comuns ao cadastro e à edição da mídia.</summary>
internal static class RegrasMidia
{
    public static IRuleBuilderOptions<T, string> TituloMidia<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("O título é obrigatório.")
            .MaximumLength(150).WithMessage("O título deve ter no máximo 150 caracteres.");

    public static IRuleBuilderOptions<T, int> DuracaoMidia<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .InclusiveBetween(1, Midia.DuracaoMaximaSegundos)
            .WithMessage($"A duração deve ficar entre 1 e {Midia.DuracaoMaximaSegundos} segundos.");

    /// <summary>O fim, quando informado com o início, não pode ser antes dele (o erro cai no campo do fim).</summary>
    public static IRuleBuilderOptions<T, T> PeriodoMidia<T>(
        this IRuleBuilder<T, T> regra,
        Func<T, DateOnly?> inicio,
        Func<T, DateOnly?> fim) =>
        regra
            .Must(x => inicio(x) is not { } i || fim(x) is not { } f || f >= i)
            .WithName("FimExibicao")
            .OverridePropertyName("FimExibicao")
            .WithMessage("O fim da exibição não pode ser antes do início.");
}
