namespace Atlas.Application.Platform.Smtp.Commands.UpsertSmtpConfiguration;

public class UpsertSmtpConfigurationCommandValidator : AbstractValidator<UpsertSmtpConfigurationCommand>
{
    public UpsertSmtpConfigurationCommandValidator()
    {
        RuleFor(v => v.Host)
            .NotEmpty().WithMessage("O Host do servidor SMTP é obrigatório.")
            .MaximumLength(255).WithMessage("O Host não pode exceder 255 caracteres.");

        RuleFor(v => v.Port)
            .GreaterThan(0).WithMessage("A Porta deve ser um número maior que 0.")
            .LessThan(65536).WithMessage("A Porta informada é inválida para conexões de rede.");

        RuleFor(v => v.UserName)
            .NotEmpty().WithMessage("O Usuário de autenticação é obrigatório.")
            .MaximumLength(255).WithMessage("O Usuário não pode exceder 255 caracteres.");

        // A senha só é obrigatória se o registro for novo, mas como o comando aceita vir em branco 
        // caso o usuário opte por manter a senha antiga, validamos apenas o tamanho se informada.
        RuleFor(v => v.Password)
            .MaximumLength(500).WithMessage("A Senha não pode exceder 500 caracteres.");

        RuleFor(v => v.SenderEmail)
            .NotEmpty().WithMessage("O E-mail do remetente é obrigatório.")
            .EmailAddress().WithMessage("O formato do E-mail do remetente é inválido.")
            .MaximumLength(255).WithMessage("O E-mail do remetente não pode exceder 255 caracteres.");

        RuleFor(v => v.SenderName)
            .MaximumLength(150).WithMessage("O Nome do remetente não pode exceder 150 caracteres.");
    }
}
