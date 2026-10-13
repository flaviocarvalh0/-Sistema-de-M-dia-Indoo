namespace Atlas.Application.Platform.EntityAttachments.Common;

public sealed record CreateEntityAttachmentRequest
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
