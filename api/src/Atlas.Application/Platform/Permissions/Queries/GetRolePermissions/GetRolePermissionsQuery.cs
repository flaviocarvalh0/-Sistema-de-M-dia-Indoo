using Atlas.Application.Common.Extensions;
using Atlas.Application.Platform.Permissions.Dtos;

namespace Atlas.Application.Platform.Permissions.Queries.GetRolePermissions;

/// <summary>
/// Consulta responsável por solicitar a árvore lógica e dinâmica de permissões de um Grupo (Role) específico.
/// </summary>
public record GetRolePermissionsQuery : IRequest<RolePermissionsDto?>
{
    public long RoleId { get; init; }
}

/// <summary>
/// Manipulador responsável por correlacionar os módulos e suas entidades associadas,
/// aplicando o mapeamento de claims conforme as permissões persistidas no banco.
/// </summary>
public class GetRolePermissionsQueryHandler : IRequestHandler<GetRolePermissionsQuery, RolePermissionsDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IPermissionService _permissionService;

    public GetRolePermissionsQueryHandler(IApplicationDbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<RolePermissionsDto?> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Resolve o nome real do grupo
        var roleName = await _permissionService.GetRoleNameAsync(request.RoleId, cancellationToken);
        if (roleName == null) return null;

        // 2. Busca as claims persistidas
        var roleClaims = await _permissionService.GetRolePermissionsAsync(request.RoleId, cancellationToken);

        // 3. Carrega módulos com suas entidades estritamente associadas (evita entidades órfãs)
        var todosModulos = await _context.SystemModules
            .AsNoTracking()
            .Include(m => m.Entities)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var modulesDto = new List<ModulePermissionDto>();

        // 4. Montagem dinâmica da árvore
        foreach (var modulo in todosModulos)
        {
            modulesDto.Add(new ModulePermissionDto
            {
                ModuleId = modulo.Id,
                ModuleName = modulo.Name,
                IsModuleEnabled = roleClaims.Any(rc => rc.ModuleId == modulo.Id),
                // Mapeia apenas o que está vinculado ao módulo
                Entities = modulo.Entities?.Select(meta => MapearParaEntityPermissionDto(meta, roleClaims)).ToList() ?? new List<EntityPermissionDto>()
            });
        }

        return new RolePermissionsDto
        {
            RoleId = request.RoleId,
            TargetName = roleName,
            Modules = modulesDto
        };
    }

    /// <summary>
    /// Cruza os metadados com as claims, gerando o prefixo padronizado (PascalCase+Plural)
    /// e validando os estados de permissão.
    /// </summary>
    private static EntityPermissionDto MapearParaEntityPermissionDto(
        Atlas.Domain.Platform.Entities.EntityMetadata meta,
        List<PersistedPermissionClaimDto> roleClaims)
    {
        // Gera o prefixo conforme convenção (ex: "produto_servico" -> "ProdutoServicos")
        var prefixoPermissao = meta.TableName.ToPascalCasePlural();

        var claimsDaEntidade = roleClaims
            .Where(rc => rc.EntityName == meta.TableName)
            .Select(rc => rc.ClaimValue)
            .ToList();

        return new EntityPermissionDto
        {
            EntityMetadataId = meta.Id,
            DisplayLabel = meta.DisplayLabel ?? meta.TableName,
            PhysicalTableName = meta.TableName,
            PermissionPrefix = prefixoPermissao,
            IsEntityEnabled = claimsDaEntidade.Any(),

            // Valida permissões considerando o novo prefixo padronizado e retrocompatibilidade
            CanView = claimsDaEntidade.Contains($"{prefixoPermissao}.View") || claimsDaEntidade.Contains($"{meta.TableName}.View"),
            CanCreate = claimsDaEntidade.Contains($"{prefixoPermissao}.Create") || claimsDaEntidade.Contains($"{meta.TableName}.Create"),
            CanUpdate = claimsDaEntidade.Contains($"{prefixoPermissao}.Update") || claimsDaEntidade.Contains($"{meta.TableName}.Update"),
            CanDelete = claimsDaEntidade.Contains($"{prefixoPermissao}.Delete") || claimsDaEntidade.Contains($"{meta.TableName}.Delete")
        };
    }
}