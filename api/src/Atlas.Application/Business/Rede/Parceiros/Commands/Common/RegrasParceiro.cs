using Atlas.Application.Business.Common;

namespace Atlas.Application.Business.Rede.Parceiros.Commands.Common;

/// <summary>Regras de campo comuns ao cadastro e à edição do parceiro.</summary>
internal static class RegrasParceiro
{
    /// <summary>Teto da cota por parceiro: o plano gratuito do storage tem 1 GB no total.</summary>
    public const int CotaMaximaMb = 1024;

    public static IRuleBuilderOptions<T, string> NomeParceiro<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("O nome do parceiro é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do parceiro deve ter no máximo 150 caracteres.");

    public static IRuleBuilderOptions<T, string?> TelefoneParceiro<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .Must(t => string.IsNullOrWhiteSpace(t) || DocumentosBrasil.TelefoneValido(t))
            .WithMessage("Informe um telefone válido com DDD.");

    public static IRuleBuilderOptions<T, string?> DocumentoParceiro<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .Must(d => string.IsNullOrWhiteSpace(d) || DocumentosBrasil.DocumentoValido(d))
            .WithMessage("Informe um CPF ou CNPJ válido.");

    public static IRuleBuilderOptions<T, string?> CidadeParceiro<T>(this IRuleBuilder<T, string?> regra) =>
        regra.MaximumLength(100).WithMessage("A cidade deve ter no máximo 100 caracteres.");

    public static IRuleBuilderOptions<T, string> FusoHorarioParceiro<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .Must(FusosHorarios.Valido)
            .WithMessage("Selecione um fuso horário válido (ex.: America/Sao_Paulo).");

    public static IRuleBuilderOptions<T, int> CotaParceiro<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .InclusiveBetween(1, CotaMaximaMb)
            .WithMessage($"A cota de armazenamento deve ficar entre 1 e {CotaMaximaMb} MB.");
}
