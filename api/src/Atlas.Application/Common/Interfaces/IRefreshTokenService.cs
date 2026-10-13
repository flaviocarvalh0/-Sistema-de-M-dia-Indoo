namespace Atlas.Application.Common.Interfaces;

public interface IRefreshTokenService
{
    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
