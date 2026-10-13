namespace Atlas.Application.Platform.Users.Commands.AssignUserRole;

public class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    public AssignUserRoleCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();

        RuleFor(v => v.RoleName)
            .NotEmpty()
            .MaximumLength(256);
    }
}
