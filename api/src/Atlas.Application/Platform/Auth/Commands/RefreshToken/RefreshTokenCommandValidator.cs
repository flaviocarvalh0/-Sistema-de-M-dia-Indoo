namespace Atlas.Application.Platform.Auth.Commands.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(v => v.RefreshToken)
            .NotEmpty()
            .WithMessage("O token de atualização (Refresh Token) é obrigatório.");
    }
}
