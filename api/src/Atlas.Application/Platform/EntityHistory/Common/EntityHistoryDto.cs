namespace Atlas.Application.Platform.EntityHistory.Common;
public sealed record EntityHistoryDto
{
    public required string RouteKey { get; init; }

    public required string EntityLabel { get; init; }

    public required long RecordId { get; init; }

    public required bool HistoryEnabled { get; init; }

    public required IReadOnlyCollection<
        EntityHistoryVersionDto> Versions
    { get; init; }

    public required bool HasMore { get; init; }

    public long? NextCursor { get; init; }
}
