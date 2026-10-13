namespace Atlas.Application.Common.Models;

public class UserSessionDto
{
    public string SessionId { get; init; } = string.Empty;

    public long UserId { get; init; }
}