using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data;

namespace Atlas.Infrastructure.Identity;

public class UserSessionService : IUserSessionService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public UserSessionService(
        ApplicationDbContext context, 
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<UserSessionDto> CreateSessionAsync(
        long userId, 
        string? ipAddress, 
        string? userAgent, 
        CancellationToken cancellationToken = default)
    {
        var session = new UserSession
        {
            UserId = userId,
            SessionId = Guid.NewGuid().ToString("N"), 
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        _context.UserSessions.Add(session);

        await _context.SaveChangesAsync(cancellationToken); 

        return new UserSessionDto
        {
            UserId = session.UserId,
            SessionId = session.SessionId
        };
    }

    public async Task RevokeActiveSessionsAsync(
        long userId, 
        string reason, 
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow();

        var activeSessions = await _context.UserSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAt = utcNow; 
            session.RevokedReason = reason; 
        }

        await _context.SaveChangesAsync(cancellationToken); 
    }

    public async Task<bool> IsSessionActiveAsync(
        string sessionId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.UserSessions 
            .AnyAsync(x => 
                x.SessionId == sessionId && 
                x.RevokedAt == null, 
                cancellationToken);
    }

    public async Task RevokeSessionAsync(
        string sessionId, 
        string reason, 
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = _timeProvider.GetUtcNow();
        session.RevokedReason = reason;

        await _context.SaveChangesAsync(cancellationToken); 
    }

    public async Task SetRefreshTokenAsync(
        string sessionId,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.RefreshTokenHash = refreshTokenHash;
        session.RefreshTokenExpiresAt = refreshTokenExpiresAt;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSessionDto?> GetSessionByRefreshTokenHashAsync(
        string refreshTokenHash,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.RefreshTokenHash == refreshTokenHash &&
                x.RevokedAt == null &&
                x.RefreshTokenExpiresAt != null &&
                x.RefreshTokenExpiresAt > utcNow,
                cancellationToken);

        if (session is null)
        {
            return null;
        }

        return new UserSessionDto
        {
            UserId = session.UserId,
            SessionId = session.SessionId
        };
    }

    public async Task RotateRefreshTokenAsync(
        string sessionId,
        string newRefreshTokenHash,
        DateTimeOffset newRefreshTokenExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RefreshTokenHash = newRefreshTokenHash;
        session.RefreshTokenExpiresAt = newRefreshTokenExpiresAt;
        session.RefreshTokenRotatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
