using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Platform.EntityAttachments.Commands.UpdateEntityAttachment;

public sealed record UpdateEntityAttachmentCommand
    : IRequest<EntityAttachmentDto>
{
    public required long Id { get; init; }
    public required uint Version { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? ReferenceDate { get; init; }
}

public sealed class UpdateEntityAttachmentCommandHandler(
    IEntityAttachmentService attachmentService)
        : IRequestHandler<
        UpdateEntityAttachmentCommand,
        EntityAttachmentDto>
{
    public Task<EntityAttachmentDto> Handle(
        UpdateEntityAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        return attachmentService.UpdateDetailsAsync(
            request.Id,
            request.Version,
            request.Title,
            request.Description,
            request.ReferenceDate,
            cancellationToken);
    }
}
