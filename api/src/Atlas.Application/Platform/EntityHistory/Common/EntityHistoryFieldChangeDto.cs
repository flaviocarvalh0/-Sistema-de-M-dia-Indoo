namespace Atlas.Application.Platform.EntityHistory.Common;
public sealed record EntityHistoryFieldChangeDto
{
    public required string PropertyName { get; init; }

    public required string DisplayLabel { get; init; }

    public object? PreviousValue { get; init; }

    public object? CurrentValue { get; init; }

    public string? PreviousDisplayValue { get; init; }

    public string? CurrentDisplayValue { get; init; }
}
