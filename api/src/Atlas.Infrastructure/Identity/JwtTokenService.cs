using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.Auth.Commands.Login;
using Atlas.Domain.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsPrincipalFactory;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory)
    {
        _configuration = configuration;
        _userManager = userManager;
        _claimsPrincipalFactory = claimsPrincipalFactory;
    }

    public async Task<TokenResult> CreateTokenAsync(
        long userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new InvalidOperationException("Usuário não encontrado ao gerar token.");
        }

        var principal = await _claimsPrincipalFactory.CreateAsync(user);

        var claims = principal.Claims
            .Where(c =>
                c.Type != "amr" &&
                c.Type != "AspNet.Identity.SecurityStamp")
            .ToList();

        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        claims.Add(new Claim(CustomClaimTypes.SessionId, sessionId));

        if (user.IsSuperUser)
        {
            claims.Add(new Claim(CustomClaimTypes.Superuser, "true"));
        }

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var key = _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer não configurado.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Jwt:Audience não configurado.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Jwt:Key não configurado.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(
            int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var minutes)
                ? minutes
                : 30);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResult
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = null
        };
    }
}