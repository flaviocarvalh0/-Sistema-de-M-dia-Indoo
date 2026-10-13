namespace Atlas.Application.Platform.EntityMetadata.Commands.UpdateEntityMetadata;
public class UpdateEntityMetadataCommandValidator : AbstractValidator<UpdateEntityMetadataCommand>
{
    public UpdateEntityMetadataCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0).WithMessage("O ID do metadado é obrigatório.");

        RuleFor(v => v.DisplayLabel)
            .NotEmpty().WithMessage("O rótulo de exibição (DisplayLabel) é obrigatório.")
            .MaximumLength(150).WithMessage("O rótulo de exibição deve ter no máximo 150 caracteres.");

        RuleFor(v => v.DefaultPageSize)
            .GreaterThan(0).WithMessage("O tamanho de página padrão deve ser maior que zero.")
            .LessThanOrEqualTo(1000).WithMessage("O tamanho de página máximo permitido é 1000.");

        RuleFor(v => v.CodePrefix)
            .MaximumLength(20).WithMessage("O prefixo de código deve ter no máximo 20 caracteres.")
            .When(v => !string.IsNullOrWhiteSpace(v.CodePrefix));

        RuleFor(v => v.Version)
            .GreaterThan(0u).WithMessage("A versão (xmin) do registro é obrigatória para o controle de concorrência.");
    }
}
