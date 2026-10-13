namespace Atlas.Application.Platform.Users.Commands.DeactivateUser;

public class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();
    }
}
