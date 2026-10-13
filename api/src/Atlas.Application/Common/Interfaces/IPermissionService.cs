using Atlas.Application.Platform.Permissions.Dtos;

namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Contrato para o serviço de gerenciamento e persistência da árvore de permissões dinâmicas do ecossistema Atlas.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Recupera o nome amigável de um Grupo (Role) direto da base de dados do Identity.
    /// </summary>
    Task<string?> GetRoleNameAsync(long roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera todas as claims de permissão associadas a um Grupo (Role) específico.
    /// </summary>
    Task<List<PersistedPermissionClaimDto>> GetRolePermissionsAsync(long roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera todas as claims de permissão associadas diretamente a um Usuário específico.
    /// </summary>
    Task<List<PersistedPermissionClaimDto>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza em lote as permissões vinculadas a um Grupo (Role).
    /// </summary>
    Task UpdateRolePermissionsAsync(long roleId, List<ModulePermissionInputDto> modules, CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza em lote as permissões vinculadas diretamente a um Usuário.
    /// </summary>
    Task UpdateUserPermissionsAsync(long userId, List<ModulePermissionInputDto> modules, CancellationToken cancellationToken);

    Task<string?> GetUserNameAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Retorna os IDs dos módulos aos quais o usuário tem acesso (direto ou via grupo).
    /// </summary>
    Task<List<long>> GetAllowedModuleIdsAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Recupera a lista de identificadores dos Grupos (Roles) aos quais um Usuário específico está vinculado.
    /// </summary>
    Task<List<long>> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken);
}
