namespace Atlas.Application.Platform.EntityHistory.Common;
public sealed record EntityHistoryVersionDto
{
    public required long HistoryId { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public DateTimeOffset? ChangedAt { get; init; }

    public string? ChangedById { get; init; }

    public string? ChangedByName { get; init; }

    public required bool IsInitialVersion { get; init; }

    public required IReadOnlyCollection<
        EntityHistoryFieldChangeDto> Changes
    { get; init; }
}
