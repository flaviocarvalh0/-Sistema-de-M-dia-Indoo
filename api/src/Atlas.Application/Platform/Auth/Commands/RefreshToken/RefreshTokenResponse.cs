namespace Atlas.Application.Platform.Auth.Commands.RefreshToken;

public class RefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }
}
