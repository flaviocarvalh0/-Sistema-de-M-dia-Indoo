using Atlas.Application.Platform.Auth.Commands.Login;
using Atlas.Application.Platform.Auth.Commands.RefreshToken;

namespace Atlas.Application.Common.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Realiza a autenticação de um usuário na plataforma utilizando o modo de login informado.
    /// </summary>
    /// <param name="userName">Nome de usuário utilizado para autenticação.</param>
    /// <param name="password">Senha informada pelo usuário.</param>
    /// <param name="mode">
    /// Modo de autenticação desejado, determinando se a sessão será estabelecida por cookie ou por tokens JWT.
    /// </param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>
    /// Resultado contendo os dados do login, incluindo informações do usuário, necessidade de troca de senha,
    /// exigência de autenticação em dois fatores e, quando aplicável, tokens de acesso e renovação.
    /// </returns>
    Task<Result<LoginResponse>> LoginAsync(
        string userName,
        string password,
        LoginMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renova a autenticação do usuário a partir de um refresh token válido.
    /// </summary>
    /// <param name="refreshToken">Refresh token emitido anteriormente para a sessão do usuário.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>
    /// Resultado contendo um novo access token, um novo refresh token e a data de expiração do access token.
    /// </returns>
    Task<Result<RefreshTokenResponse>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(
        long userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(
        CancellationToken cancellationToken = default);
}
