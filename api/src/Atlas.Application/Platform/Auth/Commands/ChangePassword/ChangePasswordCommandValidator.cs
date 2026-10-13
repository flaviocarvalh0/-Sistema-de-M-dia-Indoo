namespace Atlas.Application.Platform.Auth.Commands.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(v => v.CurrentPassword)
            .NotEmpty();

        RuleFor(v => v.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .NotEqual(c => c.CurrentPassword)
            .WithMessage("A nova senha deve ser diferente da senha temporária atual.");

        RuleFor(v => v.ConfirmNewPassword)
            .Equal(v => v.NewPassword)
            .WithMessage("A confirmação da senha não confere.");
    }
}
