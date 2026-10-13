namespace Atlas.Application.Platform.EntityAttachments
    .Queries.GetEntityAttachmentContent;

public sealed class GetEntityAttachmentContentQueryValidator
    : AbstractValidator<GetEntityAttachmentContentQuery>
{
    public GetEntityAttachmentContentQueryValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0)
            .WithMessage(
                "O identificador do anexo deve ser maior que zero.");
    }
}
