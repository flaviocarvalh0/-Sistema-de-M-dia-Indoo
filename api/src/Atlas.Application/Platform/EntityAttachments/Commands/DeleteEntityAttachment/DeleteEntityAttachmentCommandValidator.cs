namespace Atlas.Application.Platform.EntityAttachments
    .Commands.DeleteEntityAttachment;

public sealed class DeleteEntityAttachmentCommandValidator
    : AbstractValidator<DeleteEntityAttachmentCommand>
{
    public DeleteEntityAttachmentCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithMessage(
                "O identificador do anexo deve ser maior que zero.");

        RuleFor(command => command.Version)
            .GreaterThan(0u)
            .WithMessage(
                "A versão do anexo é obrigatória.");
    }
}
