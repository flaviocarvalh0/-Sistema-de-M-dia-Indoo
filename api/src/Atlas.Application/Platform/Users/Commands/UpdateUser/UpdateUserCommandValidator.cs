namespace Atlas.Application.Platform.Users.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();

        RuleFor(v => v.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Email)
            .EmailAddress()
            .When(v => !string.IsNullOrWhiteSpace(v.Email));
    }
}
