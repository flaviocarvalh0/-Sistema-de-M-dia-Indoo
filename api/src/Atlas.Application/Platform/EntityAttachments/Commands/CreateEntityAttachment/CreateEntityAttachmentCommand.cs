using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Platform.EntityAttachments
    .Commands.CreateEntityAttachment;

public sealed record CreateEntityAttachmentCommand
    : IRequest<EntityAttachmentDto>
{
    public required string RouteKey { get; init; }
    public required long RecordId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }

    [LogIgnore]
    public required byte[] Content { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? ReferenceDate { get; init; }
}

public sealed class CreateEntityAttachmentCommandHandler(
    IEntityAttachmentService attachmentService)
        : IRequestHandler<
        CreateEntityAttachmentCommand,
        EntityAttachmentDto>
{
    public Task<EntityAttachmentDto> Handle(
        CreateEntityAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        return attachmentService.CreateAsync(
            new CreateEntityAttachmentRequest
            {
                RouteKey = request.RouteKey,
                RecordId = request.RecordId,
                FileName = request.FileName,
                ContentType = request.ContentType,
                Content = request.Content,
                Title = request.Title,
                Description = request.Description,
                ReferenceDate = request.ReferenceDate
            },
            cancellationToken);
    }
}
