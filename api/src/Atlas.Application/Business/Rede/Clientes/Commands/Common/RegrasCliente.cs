using Atlas.Application.Business.Common;

namespace Atlas.Application.Business.Rede.Clientes.Commands.Common;

/// <summary>Regras de campo comuns ao cadastro e à edição do cliente.</summary>
internal static class RegrasCliente
{
    /// <summary>Teto da cota por cliente: o plano gratuito do storage tem 1 GB no total.</summary>
    public const int CotaMaximaMb = 1024;

    public static IRuleBuilderOptions<T, string> NomeCliente<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("O nome do cliente é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do cliente deve ter no máximo 150 caracteres.");

    public static IRuleBuilderOptions<T, string?> TelefoneCliente<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .Must(t => string.IsNullOrWhiteSpace(t) || DocumentosBrasil.TelefoneValido(t))
            .WithMessage("Informe um telefone válido com DDD.");

    public static IRuleBuilderOptions<T, string?> DocumentoCliente<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .Must(d => string.IsNullOrWhiteSpace(d) || DocumentosBrasil.DocumentoValido(d))
            .WithMessage("Informe um CPF ou CNPJ válido.");

    public static IRuleBuilderOptions<T, string?> CidadeCliente<T>(this IRuleBuilder<T, string?> regra) =>
        regra.MaximumLength(100).WithMessage("A cidade deve ter no máximo 100 caracteres.");

    public static IRuleBuilderOptions<T, string> FusoHorarioCliente<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .Must(FusosHorarios.Valido)
            .WithMessage("Selecione um fuso horário válido (ex.: America/Sao_Paulo).");

    public const int LimiteMidiasAtivasMaximo = 1000;

    public static IRuleBuilderOptions<T, int> LimiteMidiasAtivas<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .InclusiveBetween(1, LimiteMidiasAtivasMaximo)
            .WithMessage($"O limite de mídias ativas deve ficar entre 1 e {LimiteMidiasAtivasMaximo}.");

    public static IRuleBuilderOptions<T, int> CotaCliente<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .InclusiveBetween(1, CotaMaximaMb)
            .WithMessage($"A cota de armazenamento deve ficar entre 1 e {CotaMaximaMb} MB.");
}
