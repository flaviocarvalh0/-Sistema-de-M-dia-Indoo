using Atlas.Application.Platform.Permissions.Dtos;

namespace Atlas.Application.Platform.Permissions.Commands.UpdateRolePermissions;

/// <summary>
/// Comando responsável por solicitar a atualização em lote da matriz de permissões de um grupo específico.
/// </summary>
public record UpdateRolePermissionsCommand : IRequest<bool>
{
    /// <summary>
    /// Identificador único do grupo (Role) que receberá a nova árvore de permissões.
    /// </summary>
    public long RoleId { get; init; }

    /// <summary>
    /// Lista contendo a estrutura de módulos e suas respectivas entidades configuradas.
    /// </summary>
    public List<ModulePermissionInputDto> Modules { get; init; } = [];
}

/// <summary>
/// Manipulador (Handler) responsável por orquestrar o processo de limpeza e gravação atômica de permissões.
/// </summary>
public class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPermissionService _permissionService;

    public UpdateRolePermissionsCommandHandler(
        IApplicationDbContext context,
        IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<bool> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        // 1. Delega o lote completo para a infraestrutura preparar as claims na memória normalizando plurais e metadados
        await _permissionService.UpdateRolePermissionsAsync(request.RoleId, request.Modules, cancellationToken);

        // 2. Persiste o bloco de operações de forma 100% atômica no banco físico (Unit of Work)
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}