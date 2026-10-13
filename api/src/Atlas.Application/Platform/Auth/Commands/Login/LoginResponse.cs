namespace Atlas.Application.Platform.Auth.Commands.Login;

public class LoginResponse
{
    public bool Succeeded { get; init; }

    public bool MustChangePassword { get; init; }

    public bool RequiresTwoFactor { get; init; }

    [LogSensitive]
    public string? AccessToken { get; init; }

    [LogSensitive]
    public string? RefreshToken { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public UserDto? User { get; init; }
}
