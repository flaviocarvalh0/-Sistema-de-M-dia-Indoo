namespace Atlas.Application.Platform.Users.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("O nome completo é obrigatório.")
            .MaximumLength(200).WithMessage("O nome completo deve ter no máximo 200 caracteres.");

        RuleFor(v => v.UserName)
            .NotEmpty().WithMessage("O nome de usuário é obrigatório.")
            .MinimumLength(3).WithMessage("O nome de usuário deve ter pelo menos 3 caracteres.")
            .MaximumLength(200).WithMessage("O nome de usuário deve ter no máximo 200 caracteres.");

        RuleFor(v => v.Email)
            .EmailAddress().WithMessage("O formato do e-mail é inválido.")
            .When(v => !string.IsNullOrWhiteSpace(v.Email));

        RuleFor(v => v.TemporaryPassword)
            .NotEmpty().WithMessage("A senha temporária é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(100).WithMessage("A senha deve ter no máximo 100 caracteres.")

            .Matches(@"[A-Z]").WithMessage("A senha deve conter pelo menos uma letra maiúscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter pelo menos uma letra minúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter pelo menos um dígito.");
    }
}
