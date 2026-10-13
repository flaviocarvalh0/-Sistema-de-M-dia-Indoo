namespace Atlas.Domain.Platform.Entities;

public class UserSession : BaseAuditableEntity
{
    public long UserId { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string? RefreshTokenHash { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public DateTimeOffset? RefreshTokenRotatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsActive => RevokedAt is null;

    public bool HasValidRefreshToken(DateTimeOffset utcNow)
    {
        return RevokedAt is null && 
            RefreshTokenHash is not null && 
            RefreshTokenExpiresAt is not null && 
            RefreshTokenExpiresAt > utcNow;
    }
}
