namespace Atlas.Application.Platform.EntityAttachments
    .Queries.GetEntityAttachments;

public sealed class GetEntityAttachmentsQueryValidator
    : AbstractValidator<GetEntityAttachmentsQuery>
{
    public GetEntityAttachmentsQueryValidator()
    {
        RuleFor(query => query.RouteKey)
            .NotEmpty()
            .WithMessage(
                "A chave de rota da entidade é obrigatória.")
            .MaximumLength(150)
            .WithMessage(
                "A chave de rota da entidade não pode exceder 150 caracteres.");

        RuleFor(query => query.RecordId)
            .GreaterThan(0)
            .WithMessage(
                "O identificador do registro deve ser maior que zero.");
    }
}
