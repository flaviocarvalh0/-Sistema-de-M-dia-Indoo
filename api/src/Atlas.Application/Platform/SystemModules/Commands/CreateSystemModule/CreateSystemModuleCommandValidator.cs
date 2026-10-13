namespace Atlas.Application.Platform.SystemModules.Commands.CreateSystemModule;
public class CreateSystemModuleCommandValidator : AbstractValidator<CreateSystemModuleCommand>
{
    public CreateSystemModuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do módulo é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do módulo deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(250).WithMessage("A descrição do módulo deve ter no máximo 250 caracteres.");

        RuleFor(x => x.RoutePrefix)
            .NotEmpty().WithMessage("O prefixo de rota é obrigatório.")
            .MaximumLength(100).WithMessage("O prefixo de rota deve ter no máximo 100 caracteres.")
            .Matches(@"^/?[a-zA-Z0-9\-\/]+$")
            .WithMessage("O prefixo de rota deve conter apenas letras, números, hífen e barra.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A ordem de exibição não pode ser um número negativo.");

        RuleFor(x => x.EntityMetadataIds)
            .NotNull().WithMessage("A lista de entidades não pode ser nula.")
            .Must(x => x != null && x.Count > 0)
            .WithMessage("Informe ao menos uma entidade para associar ao novo módulo.");
    }
}
