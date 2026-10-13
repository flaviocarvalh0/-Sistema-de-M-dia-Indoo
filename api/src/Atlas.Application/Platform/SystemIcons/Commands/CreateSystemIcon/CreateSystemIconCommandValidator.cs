namespace Atlas.Application.Platform.SystemIcons.Commands.CreateSystemIcon;

public class CreateSystemIconCommandValidator : AbstractValidator<CreateSystemIconCommand>
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/svg+xml",
        "image/png",
        "image/webp",
        "image/jpeg"
    ];

    public CreateSystemIconCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithMessage("Tipo de arquivo não permitido para ícone.");

        RuleFor(x => x.Extension)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(x => x.SizeInBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(250 * 1024)
            .WithMessage("O ícone deve possuir no máximo 250 KB.");

        RuleFor(x => x.Data)
            .NotEmpty()
            .Must(data => data != null && data.Length <= 250 * 1024)
            .WithMessage("O ícone deve possuir no máximo 250 KB.");
    }
}
