namespace Atlas.Application.Platform.Roles.Commands.UpdateRole;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("O ID do grupo é obrigatório.");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("O nome do grupo é obrigatório.")
            .MaximumLength(256).WithMessage("O nome do grupo não pode exceder 256 caracteres.");
    }
}