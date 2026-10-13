namespace Atlas.Application.Common.Interfaces;

public interface IUserSessionService
{
    Task<UserSessionDto> CreateSessionAsync(
        long userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task RevokeActiveSessionsAsync(
        long userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> IsSessionActiveAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(
        string sessionId,
        string reason,
        CancellationToken cancellationToken = default);

    Task SetRefreshTokenAsync(
        string sessionId,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken cancellationToken = default);

    Task<UserSessionDto?> GetSessionByRefreshTokenHashAsync(
        string refreshTokenHash,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task RotateRefreshTokenAsync(
        string sessionId,
        string newRefreshTokenHash,
        DateTimeOffset newRefreshTokenExpiresAt,
        CancellationToken cancellationToken = default);
}
