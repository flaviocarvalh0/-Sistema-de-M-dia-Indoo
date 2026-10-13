namespace Atlas.Application.Platform.EntityAttachments.Common;

public sealed record EntityAttachmentDto
{
    public required long Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required string Extension { get; init; }
    public required long Size { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? ReferenceDate { get; init; }
    public required DateTimeOffset Created { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public long? LastModifiedBy { get; init; }
    public string? LastModifiedByName { get; init; }
    public required uint Version { get; init; }
}
