using Atlas.Application.Platform.Roles.Dtos;

namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Define operações para gerenciamento de grupos/perfis de acesso da plataforma.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Verifica se já existe um grupo com o nome informado.
    /// </summary>
    Task<bool> RoleExistsAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo grupo de acesso.
    /// </summary>
    Task<Result<long>> CreateRoleAsync(
        string roleName,
        long createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todos os grupos cadastrados.
    /// </summary>
    Task<IReadOnlyCollection<RoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna um grupo pelo identificador informado.
    /// </summary>
    Task<RoleDto?> GetRoleByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza nome e status de ativação de um grupo existente.
    /// </summary>
    Task<Result> UpdateRoleAsync(
        long id,
        string roleName,
        bool isActive,
        long updatedBy,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateRolePermissionsAsync(
        long roleId, 
        IEnumerable<string> permissions, 
        CancellationToken cancellationToken = default);

    Task<Result> DeleteRoleAsync(long roleId, CancellationToken cancellationToken);
}
