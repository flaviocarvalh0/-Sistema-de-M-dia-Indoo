namespace Atlas.Application.Platform.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(v => v.UserName)
            .NotEmpty()
            .WithName("Usuário")
            .WithMessage("'{PropertyName}' deve ser informado.")
            .MaximumLength(256)
            .WithMessage("'{PropertyName}' não pode exceder 256 caracteres.");

        RuleFor(v => v.Password)
            .NotEmpty()
            .WithName("Senha")
            .WithMessage("'{PropertyName}' deve ser informada.");
    }
}
