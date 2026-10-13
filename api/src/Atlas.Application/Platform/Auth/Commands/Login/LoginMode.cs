namespace Atlas.Application.Platform.Auth.Commands.Login;

/// <summary>
/// Define o modo de autenticação utilizado na requisição.
/// </summary>
public enum LoginMode
{
    /// <summary>
    /// Autenticação baseada em cookies (sessões do navegador).
    /// </summary>
    Cookie = 1,

    /// <summary>
    /// Autenticação baseada em token JWT (cabeçalho Authorization).
    /// </summary>
    Bearer = 2
}
