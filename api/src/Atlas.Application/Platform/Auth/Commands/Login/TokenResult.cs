namespace Atlas.Application.Platform.Auth.Commands.Login;

public class TokenResult
{
    public string AccessToken { get; init; } = string.Empty;

    public string? RefreshToken { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
