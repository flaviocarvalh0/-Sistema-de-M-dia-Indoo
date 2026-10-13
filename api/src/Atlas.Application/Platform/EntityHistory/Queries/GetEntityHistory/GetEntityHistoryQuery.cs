using Atlas.Application.Platform.EntityHistory.Common;

namespace Atlas.Application.Platform.EntityHistory.Queries.GetEntityHistory;
public sealed record GetEntityHistoryQuery
    : IRequest<EntityHistoryDto>
{
    public required string RouteKey { get; init; }

    public required long RecordId { get; init; }

    public long? BeforeHistoryId { get; init; }

    public int PageSize { get; init; } = 100;
}

public sealed class GetEntityHistoryQueryHandler
    : IRequestHandler<GetEntityHistoryQuery, EntityHistoryDto>
{
    private readonly IEntityHistoryQueryService _historyQueryService;

    public GetEntityHistoryQueryHandler(
        IEntityHistoryQueryService historyQueryService)
    {
        _historyQueryService = historyQueryService;
    }

    public Task<EntityHistoryDto> Handle(
        GetEntityHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return _historyQueryService.GetAsync(
            request.RouteKey,
            request.RecordId,
            request.BeforeHistoryId,
            request.PageSize,
            cancellationToken);
    }
}
