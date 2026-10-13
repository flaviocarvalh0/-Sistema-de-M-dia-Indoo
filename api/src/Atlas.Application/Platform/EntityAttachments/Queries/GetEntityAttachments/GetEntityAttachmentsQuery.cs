using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Platform.EntityAttachments
    .Queries.GetEntityAttachments;

public sealed record GetEntityAttachmentsQuery
    : IRequest<IReadOnlyCollection<EntityAttachmentDto>>
{
    public required string RouteKey { get; init; }
    public required long RecordId { get; init; }
}

public sealed class GetEntityAttachmentsQueryHandler(
    IEntityAttachmentService attachmentService)
        : IRequestHandler<
        GetEntityAttachmentsQuery,
        IReadOnlyCollection<EntityAttachmentDto>>
{
    public Task<IReadOnlyCollection<EntityAttachmentDto>> Handle(
        GetEntityAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        return attachmentService.GetAsync(
            request.RouteKey,
            request.RecordId,
            cancellationToken);
    }
}
