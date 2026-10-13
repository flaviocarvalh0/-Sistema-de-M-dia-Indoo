using Atlas.Application.Common.Extensions;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.Permissions.Dtos;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Data;

namespace Atlas.Infrastructure.Identity;

/// <summary>
/// Serviço de infraestrutura responsável pelo gerenciamento, sincronização 
/// e persistência da árvore de permissões dinâmicas (Claims) vinculadas a Usuários e Grupos (Roles).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetRoleNameAsync(long roleId, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        return role?.Name;
    }

    public async Task<string?> GetUserNameAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.UserName;
    }

    public async Task<List<PersistedPermissionClaimDto>> GetRolePermissionsAsync(long roleId, CancellationToken cancellationToken)
    {
        return await _context.RoleClaims
            .AsNoTracking()
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == CustomClaimTypes.Permission)
            .Select(rc => new PersistedPermissionClaimDto(
                rc.ClaimValue ?? string.Empty,
                rc.ModuleId,
                rc.EntityName))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersistedPermissionClaimDto>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken)
    {
        return await _context.UserClaims
            .AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.ClaimType == CustomClaimTypes.Permission)
            .Select(uc => new PersistedPermissionClaimDto(
                uc.ClaimValue ?? string.Empty,
                uc.ModuleId,
                uc.EntityName))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(long roleId, List<ModulePermissionInputDto> modules, CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();

        // Identifica os módulos contidos no payload para limpar apenas o escopo deles
        var moduleIdsToUpdate = modules.Select(m => m.ModuleId).Distinct().ToList();

        var claimsAntigas = await _context.RoleClaims
            .Where(rc => rc.RoleId == roleId
                      && rc.ClaimType == CustomClaimTypes.Permission
                      && moduleIdsToUpdate.Contains(rc.ModuleId ?? 0))
            .ToListAsync(cancellationToken);

        if (claimsAntigas.Any())
        {
            _context.RoleClaims.RemoveRange(claimsAntigas);
        }

        foreach (var modulo in modules)
        {
            foreach (var entidade in modulo.Entities)
            {
                var acoes = new List<(bool Permitido, string Sufixo)>
                {
                    (entidade.CanView, "View"),
                    (entidade.CanCreate, "Create"),
                    (entidade.CanUpdate, "Update"),
                    (entidade.CanDelete, "Delete")
                };

                foreach (var acao in acoes.Where(a => a.Permitido))
                {
                    var prefixoPermissao = entidade.PhysicalTableName.ToPascalCasePlural();

                    var novaClaim = new ApplicationRoleClaim
                    {
                        RoleId = roleId,
                        ClaimType = CustomClaimTypes.Permission,
                        ClaimValue = $"{prefixoPermissao}.{acao.Sufixo}",
                        ModuleId = modulo.ModuleId,
                        EntityName = PatternPhysicalName(entidade.PhysicalTableName)
                    };

                    _context.Entry(novaClaim).State = EntityState.Added;
                }
            }
        }
    }

    public async Task UpdateUserPermissionsAsync(long userId, List<ModulePermissionInputDto> modules, CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();

        // Identifica os módulos contidos no payload para limpar apenas o escopo deles
        var moduleIdsToUpdate = modules.Select(m => m.ModuleId).Distinct().ToList();

        var claimsAntigas = await _context.UserClaims
            .Where(uc => uc.UserId == userId
                      && uc.ClaimType == CustomClaimTypes.Permission
                      && moduleIdsToUpdate.Contains(uc.ModuleId ?? 0))
            .ToListAsync(cancellationToken);

        if (claimsAntigas.Any())
        {
            _context.UserClaims.RemoveRange(claimsAntigas);
        }

        foreach (var modulo in modules)
        {
            foreach (var entidade in modulo.Entities)
            {
                var acoes = new List<(bool Permitido, string Sufixo)>
                {
                    (entidade.CanView, "View"),
                    (entidade.CanCreate, "Create"),
                    (entidade.CanUpdate, "Update"),
                    (entidade.CanDelete, "Delete")
                };

                foreach (var acao in acoes.Where(a => a.Permitido))
                {
                    var prefixoPermissao = entidade.PhysicalTableName.ToPascalCasePlural();

                    var novaClaim = new ApplicationUserClaim
                    {
                        UserId = userId,
                        ClaimType = CustomClaimTypes.Permission,
                        ClaimValue = $"{prefixoPermissao}.{acao.Sufixo}",
                        ModuleId = modulo.ModuleId,
                        EntityName = PatternPhysicalName(entidade.PhysicalTableName)
                    };

                    _context.Entry(novaClaim).State = EntityState.Added;
                }
            }
        }
    }

    public async Task<List<long>> GetAllowedModuleIdsAsync(long userId, CancellationToken cancellationToken)
    {
        // 1. Verifica se o usuário é Superusuário direto no banco
        var isSuperuser = await _context.UserClaims
            .AnyAsync(uc => uc.UserId == userId
                         && uc.ClaimType == CustomClaimTypes.Superuser
                         && uc.ClaimValue == "true", cancellationToken);

        if (isSuperuser)
        {
            // Retorna todos os módulos existentes no sistema
            return await _context.SystemModules
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);
        }

        // 2. Lógica original para usuários comuns
        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        return await _context.UserClaims
            .Where(uc => uc.UserId == userId && uc.ClaimType == CustomClaimTypes.Permission && uc.ModuleId.HasValue)
            .Select(uc => uc.ModuleId!.Value)
            .Union(_context.RoleClaims
                .Where(rc => userRoleIds.Contains(rc.RoleId) && rc.ClaimType == CustomClaimTypes.Permission && rc.ModuleId.HasValue)
                .Select(rc => rc.ModuleId!.Value))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<long>> GetUserRoleIdsAsync(long userId, CancellationToken cancellationToken)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);
    }

    private static string PatternPhysicalName(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return string.Empty;
        return tableName.Trim();
    }
}
