namespace Atlas.Application.Platform.EntityHistory.Queries.GetEntityHistory;
public sealed class GetEntityHistoryQueryValidator
    : AbstractValidator<GetEntityHistoryQuery>
{
    public GetEntityHistoryQueryValidator()
    {
        RuleFor(query => query.RouteKey)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(query => query.RecordId)
            .GreaterThan(0);

        RuleFor(query => query.BeforeHistoryId)
            .GreaterThan(0)
            .When(query => query.BeforeHistoryId.HasValue);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 200);
    }
}
