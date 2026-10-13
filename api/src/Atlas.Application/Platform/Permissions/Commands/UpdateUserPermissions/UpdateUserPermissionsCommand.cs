using Atlas.Application.Platform.Permissions.Dtos;

namespace Atlas.Application.Platform.Permissions.Commands.UpdateUserPermissions;

/// <summary>
/// Comando responsável por solicitar a atualização em lote da matriz de permissões diretas de um usuário específico.
/// </summary>
public record UpdateUserPermissionsCommand : IRequest<bool>
{
    /// <summary>
    /// Identificador único do usuário que receberá a árvore de permissões diretas customizadas.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// Lista contendo a estrutura de módulos e suas respectivas entidades configuradas para o usuário.
    /// Reutiliza os DTOs de entrada estruturados no escopo de permissões.
    /// </summary>
    public List<ModulePermissionInputDto> Modules { get; init; } = [];
}

/// <summary>
/// Manipulador (Handler) responsável por orquestrar o processo de limpeza e gravação atômica de permissões diretas do usuário.
/// </summary>
public class UpdateUserPermissionsCommandHandler : IRequestHandler<UpdateUserPermissionsCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPermissionService _permissionService;

    public UpdateUserPermissionsCommandHandler(
        IApplicationDbContext context,
        IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<bool> Handle(UpdateUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        // 1. Repassa o payload completo de uma vez só para a infraestrutura preparar o estado na memória limpando o rastreador
        await _permissionService.UpdateUserPermissionsAsync(request.UserId, request.Modules, cancellationToken);

        // 2. Comita as remoções e inserções em uma única chamada atômica ao banco (Unit of Work)
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}