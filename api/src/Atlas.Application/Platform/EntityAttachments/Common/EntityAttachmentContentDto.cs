namespace Atlas.Application.Platform.EntityAttachments.Common;

public sealed record EntityAttachmentContentDto
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }

    [LogIgnore]
    public required byte[] Content { get; init; }
}
