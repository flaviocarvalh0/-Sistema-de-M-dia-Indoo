namespace Atlas.Application.Platform.EntityAttachments.Commands.UpdateEntityAttachment;

public sealed class UpdateEntityAttachmentCommandValidator
    : AbstractValidator<UpdateEntityAttachmentCommand>
{
    public UpdateEntityAttachmentCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithMessage(
                "O identificador do anexo deve ser maior que zero.");

        RuleFor(command => command.Version)
            .GreaterThan(0u)
            .WithMessage(
                "A versão do anexo é obrigatória.");

        RuleFor(command => command.Title)
            .MaximumLength(250)
            .WithMessage(
                "O título não pode exceder 250 caracteres.");

        RuleFor(command => command.Description)
            .MaximumLength(1000)
            .WithMessage(
                "A descrição não pode exceder 1000 caracteres.");
    }
}
