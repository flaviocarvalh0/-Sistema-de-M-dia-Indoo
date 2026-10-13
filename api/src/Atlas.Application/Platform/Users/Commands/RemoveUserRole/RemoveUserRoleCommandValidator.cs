namespace Atlas.Application.Platform.Users.Commands.RemoveUserRole;

public class RemoveUserRoleCommandValidator : AbstractValidator<RemoveUserRoleCommand>
{
    public RemoveUserRoleCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty()
            .WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(v => v.RoleName)
            .NotEmpty()
            .WithMessage("O nome do grupo de acesso é obrigatório.")
            .MaximumLength(256)
            .WithMessage("O nome do grupo de acesso deve possuir no máximo 256 caracteres.");
    }
}