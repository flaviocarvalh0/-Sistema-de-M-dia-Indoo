namespace Atlas.Application.Platform.SystemIcons.Commands.UpdateSystemIcon;

public class UpdateSystemIconCommandValidator : AbstractValidator<UpdateSystemIconCommand>
{
    public UpdateSystemIconCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("O identificador do ícone é inválido.");

        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do registro é obrigatória para atualização.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("O nome do ícone deve ser informado.")
            .MaximumLength(100)
            .WithMessage("O nome do ícone deve ter no máximo 100 caracteres.");

        When(x => x.Data is not null && x.Data.Length > 0, () =>
        {
            RuleFor(x => x.ContentType)
                .NotEmpty()
                .WithMessage("O tipo de conteúdo (ContentType) do arquivo deve ser informado.");

            RuleFor(x => x.Extension)
                .NotEmpty()
                .WithMessage("A extensão do arquivo deve ser informada.");

            RuleFor(x => x.SizeInBytes)
                .NotNull()
                .WithMessage("O tamanho do arquivo deve ser informado.")
                .GreaterThan(0)
                .WithMessage("O tamanho do arquivo deve ser maior que zero.");

            RuleFor(x => x.Data)
                .Must(data => data!.Length <= 250 * 1024)
                .WithMessage("O ícone deve possuir no máximo 250 KB.");
        });
    }
}
