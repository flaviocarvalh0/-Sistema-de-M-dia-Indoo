using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Platform.EntityAttachments
    .Commands.DeleteEntityAttachment;

public sealed record DeleteEntityAttachmentCommand(
    long Id,
    uint Version)
    : IRequest<DeleteEntityAttachmentResponse>;

public sealed class DeleteEntityAttachmentCommandHandler(
    IEntityAttachmentService attachmentService)
        : IRequestHandler<
        DeleteEntityAttachmentCommand,
        DeleteEntityAttachmentResponse>
{
    public Task<DeleteEntityAttachmentResponse> Handle(
        DeleteEntityAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        return attachmentService.DeleteAsync(
            request.Id,
            request.Version,
            cancellationToken);
    }
}
