namespace Atlas.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(long userId);

    Task<bool> IsInRoleAsync(long userId, string role);

    Task<bool> AuthorizeAsync(long userId, string policyName);

    Task<UserDto?> GetUserByIdAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<Result<long>> CreateUserAsync(
        string fullName,
        string userName,
        string? email,
        string temporaryPassword,
        bool mustChangePassword,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateUserAsync(
        long userId,
        string fullName,
        string? email,
        CancellationToken cancellationToken = default);

    Task<Result> ActivateUserAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateUserAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        long userId,
        string temporaryPassword,
        bool mustChangePassword,
        CancellationToken cancellationToken = default);

    Task<Result> AssignRoleAsync(
        long userId,
        string roleName,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveRoleAsync(
        long userId,
        string roleName,
        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(
        long userId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsSuperUserAsync(long userId);

    /// <summary>
    /// Retorna um dicionário mapeando o ID do usuário para o seu Nome Completo (ou UserName como fallback).
    /// </summary>
    Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);
}
