using Atlas.Application.Platform.Auth.Commands.Login;

namespace Atlas.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<TokenResult> CreateTokenAsync(
        long userId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
