namespace Atlas.Application.Platform.Users.Commands.ResetUserPassword;

public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty()
            .WithMessage("O identificador do usuário é obrigatório.");

        RuleFor(v => v.TemporaryPassword)
            .NotEmpty()
            .WithMessage("A senha temporária é obrigatória.")
            .MinimumLength(6)
            .WithMessage("A senha temporária deve possuir no mínimo 6 caracteres.")
            .MaximumLength(100)
            .WithMessage("A senha temporária deve possuir no máximo 100 caracteres.");

        RuleFor(v => v.ConfirmTemporaryPassword)
            .NotEmpty()
            .WithMessage("A confirmação da senha temporária é obrigatória.")
            .Equal(v => v.TemporaryPassword)
            .WithMessage("A confirmação da senha não confere.");
    }
}
