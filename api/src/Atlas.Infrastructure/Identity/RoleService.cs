using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Application.Platform.Roles.Dtos;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Atlas.Infrastructure.Identity;

public class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RoleService(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await _roleManager.RoleExistsAsync(roleName);
    }

    public async Task<Result<long>> CreateRoleAsync(string roleName, long createdBy, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var role = new ApplicationRole(roleName)
        {
            IsActive = true,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,     
            UpdatedBy = createdBy
        };

        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            return Result<long>.Failure(result.Errors.Select(e => e.Description));
        }

        return Result<long>.Success(role.Id);
    }

    public async Task<IReadOnlyCollection<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                UpdatedAt = r.UpdatedAt,
                UpdatedBy = r.UpdatedBy
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto?> GetRoleByIdAsync(
        long id, 
        CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());

        if (role is null) return null;

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            IsActive = role.IsActive,
            CreatedAt = role.CreatedAt,
            CreatedBy = role.CreatedBy,
            UpdatedAt = role.UpdatedAt,
            UpdatedBy = role.UpdatedBy
        };
    }

    public async Task<Result> UpdateRoleAsync(
        long id, 
        string roleName, 
        bool isActive, 
        long updatedBy, 
        CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());

        if (role is null)
        {
            return Result.Failure(["Grupo não encontrado."]);
        }

        role.Name = roleName;
        role.IsActive = isActive;
        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.UpdatedBy = updatedBy;

        var result = await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        return Result.Success();
    }

    public async Task<Result> UpdateRolePermissionsAsync(long roleId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return Result.Failure(["Grupo não encontrado para atualização de permissões."]);
        }

        // 1. Busca todas as claims atuais do grupo
        var existingClaims = await _roleManager.GetClaimsAsync(role);

        // Filtra apenas as claims do tipo "Permission"
        var permissionClaimsToRemove = existingClaims
            .Where(c => c.Type == CustomClaimTypes.Permission);

        // 2. Remove as antigas e adiciona as novas de forma limpa
        // Como o Identity não possui um "UpdateClaimsAsync" em lote nativo, fazemos o fluxo clássico
        foreach (var claim in permissionClaimsToRemove)
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        foreach (var permission in permissions.Distinct())
        {
            if (string.IsNullOrWhiteSpace(permission)) continue;

            var result = await _roleManager.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, permission));

            if (!result.Succeeded)
            {
                return Result.Failure(result.Errors.Select(e => e.Description));
            }
        }

        return Result.Success();
    }

    public async Task<Result> DeleteRoleAsync(long roleId, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role == null) return Result.Failure(["Grupo não encontrado."]);

        var result = await _roleManager.DeleteAsync(role);
        return result.ToApplicationResult();
    }
}