namespace Atlas.Application.Platform.Roles.Commands.CreateRole;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(v => v.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("O nome do grupo é obrigatório.")
            .MaximumLength(256)
            .WithMessage("O nome do grupo não pode exceder 256 caracteres.");
    }
}
