using Atlas.Application.Common.Extensions;
using Atlas.Application.Platform.Permissions.Dtos;

namespace Atlas.Application.Platform.Permissions.Queries.GetUserPermissions;

public record GetUserPermissionsQuery : IRequest<UserPermissionsDto?>
{
    public long UserId { get; init; }
}

public class GetUserPermissionsQueryHandler : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly IIdentityService _identityService;

    public GetUserPermissionsQueryHandler(
        IApplicationDbContext context,
        IPermissionService permissionService,
        IIdentityService identityService)
    {
        _context = context;
        _permissionService = permissionService;
        _identityService = identityService;
    }

    public async Task<UserPermissionsDto?> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Busca o nome do usuário
        var userName = await _permissionService.GetUserNameAsync(request.UserId, cancellationToken);
        if (userName == null) return null;

        // 2. Verifica se é Superadmin usando o IIdentityService
        var isSuperUser = await _identityService.IsSuperUserAsync(request.UserId);

        // 3. Busca permissões DIRETAS do usuário
        var userClaims = await _permissionService.GetUserPermissionsAsync(request.UserId, cancellationToken);

        // 4. Busca IDs dos grupos (Roles) que o usuário pertence
        var userRoleIds = await _permissionService.GetUserRoleIdsAsync(request.UserId, cancellationToken);

        var combinedClaims = new List<PersistedPermissionClaimDto>(userClaims);

        // 5. Junta as permissões de todas as Roles do usuário
        foreach (var roleId in userRoleIds)
        {
            var roleClaims = await _permissionService.GetRolePermissionsAsync(roleId, cancellationToken);
            combinedClaims.AddRange(roleClaims);
        }

        // Consolida e remove duplicidades
        var effectiveClaims = combinedClaims
            .DistinctBy(c => c.ClaimValue)
            .ToList();

        // 6. Carrega os módulos e monta a árvore
        var todosModulos = await _context.SystemModules
            .Include(m => m.Entities)
            .ToListAsync(cancellationToken);

        var modulesDto = new List<ModulePermissionDto>();

        foreach (var modulo in todosModulos)
        {
            modulesDto.Add(new ModulePermissionDto
            {
                ModuleId = modulo.Id,
                ModuleName = modulo.Name,
                IsModuleEnabled = isSuperUser || effectiveClaims.Any(uc => uc.ModuleId == modulo.Id),

                Entities = modulo.Entities?.Select(meta => MapearParaEntityPermissionDto(meta, effectiveClaims, isSuperUser))
                                           .ToList() ?? new List<EntityPermissionDto>()
            });
        }

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            TargetName = userName,
            Modules = modulesDto
        };
    }

    private static EntityPermissionDto MapearParaEntityPermissionDto(
        Atlas.Domain.Platform.Entities.EntityMetadata meta,
        List<PersistedPermissionClaimDto> effectiveClaims,
        bool isSuperUser)
    {
        var prefixoPermissao = meta.TableName.ToPascalCasePlural();

        var claimsDaEntidade = effectiveClaims
            .Where(uc => uc.EntityName == meta.TableName)
            .Select(uc => uc.ClaimValue)
            .ToList();

        return new EntityPermissionDto
        {
            EntityMetadataId = meta.Id,
            DisplayLabel = meta.DisplayLabel ?? meta.TableName,
            PhysicalTableName = meta.TableName,
            PermissionPrefix = prefixoPermissao,
            IsEntityEnabled = isSuperUser || claimsDaEntidade.Any(),

            CanView = isSuperUser || claimsDaEntidade.Contains($"{prefixoPermissao}.View") || claimsDaEntidade.Contains($"{meta.TableName}.View"),
            CanCreate = isSuperUser || claimsDaEntidade.Contains($"{prefixoPermissao}.Create") || claimsDaEntidade.Contains($"{meta.TableName}.Create"),
            CanUpdate = isSuperUser || claimsDaEntidade.Contains($"{prefixoPermissao}.Update") || claimsDaEntidade.Contains($"{meta.TableName}.Update"),
            CanDelete = isSuperUser || claimsDaEntidade.Contains($"{prefixoPermissao}.Delete") || claimsDaEntidade.Contains($"{meta.TableName}.Delete")
        };
    }
}
