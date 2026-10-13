namespace Atlas.Application.Common.Models;

public class UserDto
{
    public long Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    [LogSensitive]
    public string? Email { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsActive { get; init; }

    [LogSensitive]
    public bool MustChangePassword { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];
}
