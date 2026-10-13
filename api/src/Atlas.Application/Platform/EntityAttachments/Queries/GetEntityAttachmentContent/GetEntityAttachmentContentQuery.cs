using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Platform.EntityAttachments
    .Queries.GetEntityAttachmentContent;

public sealed record GetEntityAttachmentContentQuery(
    long Id)
    : IRequest<EntityAttachmentContentDto>;

public sealed class GetEntityAttachmentContentQueryHandler(
    IEntityAttachmentService attachmentService)
        : IRequestHandler<
        GetEntityAttachmentContentQuery,
        EntityAttachmentContentDto>
{
    public Task<EntityAttachmentContentDto> Handle(
        GetEntityAttachmentContentQuery request,
        CancellationToken cancellationToken)
    {
        return attachmentService.GetContentAsync(
            request.Id,
            cancellationToken);
    }
}
