using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Application.Platform.Auth.Commands.Login;
using Atlas.Application.Platform.Auth.Commands.RefreshToken;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Atlas.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserSessionService _userSessionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        IUserSessionService userSessionService,
        IHttpContextAccessor httpContextAccessor,
        IRefreshTokenService refreshTokenService,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _userSessionService = userSessionService;
        _httpContextAccessor = httpContextAccessor;
        _refreshTokenService = refreshTokenService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Executa o fluxo completo de login da plataforma, validando credenciais,
    /// estado do usuário, bloqueio temporário, exigência de dois fatores,
    /// criação de sessão e emissão de cookie ou tokens JWT conforme o modo informado.
    /// </summary>
    /// <param name="userName">Nome de usuário utilizado para localizar a conta.</param>
    /// <param name="password">Senha informada para validação das credenciais.</param>
    /// <param name="mode">Modo de login que define se a autenticação será feita por cookie ou JWT.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>
    /// Resultado do processo de autenticação contendo sucesso, falhas de validação ou informações adicionais
    /// necessárias para completar o login.
    /// </returns>
    public async Task<Result<LoginResponse>> LoginAsync(
       string userName,
       string password,
       LoginMode mode,
       CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(userName);

        if (user is null)
        {
            return Result<LoginResponse>.Failure(["Usuário ou senha inválidos."]);
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure(["Usuário inativo."]);
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Result<LoginResponse>.Failure(["Usuário temporariamente bloqueado."]);
        }

        if (signInResult.RequiresTwoFactor)
        {
            return Result<LoginResponse>.Success(new LoginResponse
            {
                Succeeded = false,
                RequiresTwoFactor = true,
                MustChangePassword = user.MustChangePassword
            });
        }

        if (!signInResult.Succeeded)
        {
            return Result<LoginResponse>.Failure(["Usuário ou senha inválidos."]);
        }

        await _userSessionService.RevokeActiveSessionsAsync(
            user.Id,
            "Novo login realizado.",
            cancellationToken);

        var session = await _userSessionService.CreateSessionAsync(
            user.Id,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var userDto = await _identityService.GetUserByIdAsync(
            user.Id,
            cancellationToken);

        if (mode == LoginMode.Cookie)
        {
            await _signInManager.SignInWithClaimsAsync(
                user,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                },
                new[]
                {
                    new Claim(CustomClaimTypes.SessionId, session.SessionId)   
                });

            return Result<LoginResponse>.Success(new LoginResponse
            {
                Succeeded = true,
                MustChangePassword = user.MustChangePassword,
                RequiresTwoFactor = false,
                User = userDto
            });
        }

        var refreshToken = _refreshTokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenService.HashRefreshToken(refreshToken);

        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(8);

        await _userSessionService.SetRefreshTokenAsync(
            session.SessionId,
            refreshTokenHash,
            refreshTokenExpiresAt,
            cancellationToken);

        var token = await _jwtTokenService.CreateTokenAsync(
            user.Id,
            session.SessionId,
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse
        {
            Succeeded = true,
            MustChangePassword = user.MustChangePassword,
            RequiresTwoFactor = false,
            AccessToken = token.AccessToken,
            RefreshToken = refreshToken,
            ExpiresAt = token.ExpiresAt,
            User = userDto
        });
    }

    /// <summary>
    /// Executa o fluxo de renovação de tokens da plataforma, validando o refresh token informado,
    /// verificando a sessão e o usuário associados, rotacionando o refresh token e emitindo um novo access token.
    /// </summary>
    /// <param name="refreshToken">Refresh token recebido do cliente para renovação da sessão.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>
    /// Resultado do processo de renovação contendo novos tokens quando o refresh token for válido,
    /// ou falha quando o token estiver inválido, expirado ou associado a uma sessão/usuário inválido.
    /// </returns>
    public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow();

        var refreshTokenHash = _refreshTokenService.HashRefreshToken(refreshToken);

        var session = await _userSessionService.GetSessionByRefreshTokenHashAsync(
            refreshTokenHash,
            utcNow,
            cancellationToken);

        if (session is null)
        {
            return Result<RefreshTokenResponse>.Failure(["Refresh token inválido ou expirado."]);
        }

        var user = await _userManager.FindByIdAsync(session.UserId.ToString());

        if (user is null || !user.IsActive)
        {
            await _userSessionService.RevokeSessionAsync(
                session.SessionId,
                "Usuário inexistente ou inativo durante refresh token.",
                cancellationToken);

            return Result<RefreshTokenResponse>.Failure(["Refresh token inválido ou expirado."]);
        }

        var newRefreshToken = _refreshTokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _refreshTokenService.HashRefreshToken(newRefreshToken);
        var newRefreshTokenExpiresAt = utcNow.AddHours(8);

        await _userSessionService.RotateRefreshTokenAsync(
            session.SessionId,
            newRefreshTokenHash,
            newRefreshTokenExpiresAt,
            cancellationToken);

        var token = await _jwtTokenService.CreateTokenAsync(
            user.Id,
            session.SessionId,
            cancellationToken);

        return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = token.ExpiresAt
        });
    }

    public async Task<Result> ChangePasswordAsync(
        long userId, 
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(["Usuário não encontrado."]);
        }

        if (!user.IsActive)
        {
            return Result.Failure(["Usuário inativo."]);
        }

        // 1. Preserva o estado histórico de auditoria e controle antes de qualquer modificação.
        // Isso evita corromper dados legítimos na memória caso a validação do Identity falhe.
        var originalMustChangePassword = user.MustChangePassword;
        var originalUpdatedAt = user.UpdatedAt;
        var originalUpdatedBy = user.UpdatedBy;

        // 2. Altera o estado temporariamente na memória. Como o ChangePasswordAsync do Identity 
        // efetua um SaveChanges automático internamente, essa abordagem economiza uma viagem (roundtrip) 
        // ao banco de dados, persistindo a troca da senha e as flags de auditoria em um único comando SQL.
        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = userId;

        var result = await _userManager.ChangePasswordAsync(
            user,
            currentPassword,
            newPassword);

        // 3. Se a nova senha violar as regras de complexidade do Identity, desfazemos as alterações
        // na memória para que a instância do 'user' não fique com dados inconsistentes/sujos no ChangeTracker do EF Core.
        if (!result.Succeeded)
        {
            user.MustChangePassword = originalMustChangePassword;
            user.UpdatedAt = originalUpdatedAt;
            user.UpdatedBy = originalUpdatedBy;

            return result.ToApplicationResult();
        }

        return result.ToApplicationResult();
    }

    public async Task<Result> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        var sessionId = _httpContextAccessor.HttpContext?
            .User.FindFirstValue(CustomClaimTypes.SessionId);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await _userSessionService.RevokeSessionAsync(
                sessionId,
                "Logout realizado.",
                cancellationToken);
        }

        await _signInManager.SignOutAsync();

        return Result.Success();
    }

    private string? GetIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    }
}
