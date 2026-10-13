namespace Atlas.Application.Platform.SystemModules.Commands.UpdateSystemModule;
public class UpdateSystemModuleCommandValidator : AbstractValidator<UpdateSystemModuleCommand>
{
    public UpdateSystemModuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O ID do módulo é obrigatório.");

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

        RuleFor(x => x.Version)
            .GreaterThan(0u)
            .WithMessage("A versão do registro é obrigatória para atualização.");
    }
}
