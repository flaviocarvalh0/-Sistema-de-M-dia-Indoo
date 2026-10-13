namespace Atlas.Application.Platform.EntityAttachments
    .Commands.CreateEntityAttachment;

public sealed class CreateEntityAttachmentCommandValidator
    : AbstractValidator<CreateEntityAttachmentCommand>
{
    private const long MaxFileSizeInBytes =
        100L * 1024 * 1024;

    public CreateEntityAttachmentCommandValidator()
    {
        RuleFor(command => command.RouteKey)
            .NotEmpty()
            .WithMessage(
                "A chave de rota da entidade é obrigatória.")
            .MaximumLength(150)
            .WithMessage(
                "A chave de rota da entidade não pode exceder 150 caracteres.");

        RuleFor(command => command.RecordId)
            .GreaterThan(0)
            .WithMessage(
                "O identificador do registro deve ser maior que zero.");

        RuleFor(command => command.FileName)
            .NotEmpty()
            .WithMessage(
                "O nome do arquivo é obrigatório.")
            .MaximumLength(255)
            .WithMessage(
                "O nome do arquivo não pode exceder 255 caracteres.");

        RuleFor(command => command.ContentType)
            .NotEmpty()
            .WithMessage(
                "O tipo de conteúdo do arquivo é obrigatório.")
            .MaximumLength(255)
            .WithMessage(
                "O tipo de conteúdo não pode exceder 255 caracteres.");

        RuleFor(command => command.Content)
            .NotNull()
            .WithMessage(
                "O conteúdo do arquivo é obrigatório.")
            .Must(content =>
                content is { Length: > 0 })
            .WithMessage(
                "O conteúdo do arquivo não pode estar vazio.")
            .Must(content =>
                content is null ||
                content.LongLength <= MaxFileSizeInBytes)
            .WithMessage(
                "O arquivo não pode ultrapassar 100 MB.");

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
