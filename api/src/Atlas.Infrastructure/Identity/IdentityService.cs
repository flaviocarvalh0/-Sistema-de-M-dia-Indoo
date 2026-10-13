using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _userClaimsPrincipalFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUserClaimsPrincipalFactory<ApplicationUser> userClaimsPrincipalFactory,
        IAuthorizationService authorizationService,
        IApplicationDbContext context,
        IUser user)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        _authorizationService = authorizationService;
        _context = context;
        _currentUser = user;
    }

    public async Task<bool> AuthorizeAsync(long userId, string policyName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return false;
        }

        var principal = await _userClaimsPrincipalFactory.CreateAsync(user);

        var result = await _authorizationService.AuthorizeAsync(principal, policyName);

        return result.Succeeded;
    }

    public async Task<bool> IsInRoleAsync(long userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<string?> GetUserNameAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user?.UserName;
    }

    public async Task<UserDto?> GetUserByIdAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var createdByName = user.CreatedBy.HasValue ? await GetFullNameAsync(user.CreatedBy.Value) : null;
        var updatedByName = user.UpdatedBy.HasValue ? await GetFullNameAsync(user.UpdatedBy.Value) : null;

        var roles = await _userManager.GetRolesAsync(user);

        return MapToUserDto(user, roles, createdByName, updatedByName);
    }

    public async Task<IReadOnlyCollection<UserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var result = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            result.Add(MapToUserDto(user, roles));
        }

        return result;
    }

    public async Task<Result<long>> CreateUserAsync(
         string fullName,
         string userName,
         string? email,
         string temporaryPassword,
         bool mustChangePassword,
         IEnumerable<string> roles,
         CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var currentUserId = _currentUser.Id;

            var now = DateTimeOffset.UtcNow;

            var user = new ApplicationUser
            {
                FullName = fullName,
                UserName = userName,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                EmailConfirmed = false,
                IsActive = true,
                MustChangePassword = mustChangePassword,

                CreatedAt = now,
                CreatedBy = currentUserId,
                UpdatedAt = now,
                UpdatedBy = currentUserId
            };

            var result = await _userManager.CreateAsync(user, temporaryPassword);

            if (!result.Succeeded)
            {
                return Result<long>.Failure(result.Errors.Select(e => e.Description));
            }

            foreach (var roleName in roles.Distinct())
            {
                var roleExists = await _roleManager.RoleExistsAsync(roleName);

                if (!roleExists)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<long>.Failure([$"Grupo '{roleName}' não cadastrado no sistema. Operação abortada."]);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, roleName);

                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<long>.Failure(roleResult.Errors.Select(e => e.Description));
                }
            }

            await SyncSuperUserStatusAsync(user);

            await transaction.CommitAsync(cancellationToken);
            return Result<long>.Success(user.Id);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> UpdateUserAsync(
        long userId,
        string fullName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(["Usuário não encontrado."]);
        }

        user.FullName = fullName;
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.Id;

        var result = await _userManager.UpdateAsync(user);

        return result.ToApplicationResult();
    }

    public async Task<Result> ActivateUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(["Usuário não encontrado."]);
        }

        user.IsActive = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.Id;

        var result = await _userManager.UpdateAsync(user);

        return result.ToApplicationResult();
    }

    public async Task<Result> DeactivateUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(["Usuário não encontrado."]);
        }

        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.Id;

        var result = await _userManager.UpdateAsync(user);

        return result.ToApplicationResult();
    }

    public async Task<Result> ResetPasswordAsync(
        long userId,
        string temporaryPassword,
        bool mustChangePassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(["Usuário não encontrado."]);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(
            user,
            token,
            temporaryPassword);

        if (!result.Succeeded)
        {
            return result.ToApplicationResult();
        }

        user.MustChangePassword = mustChangePassword;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.Id;

        var updateResult = await _userManager.UpdateAsync(user);

        return updateResult.ToApplicationResult();
    }

    public async Task<Result> AssignRoleAsync(long userId, string roleName, CancellationToken ct)
    {
        // 1. Defesa: Verifica se o usuário existe
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Result.Failure(["Usuário não encontrado."]);

        // 2. Defesa: Verifica se a role existe antes de tentar atribuir
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null) return Result.Failure([$"Grupo '{roleName}' não encontrado."]);

        // 3. Performance/Idempotência: Se o usuário já possui o papel, retornamos sucesso sem fazer nada
        if (await _userManager.IsInRoleAsync(user, roleName))
        {
            return Result.Success();
        }

        // 4. Execução: Adiciona o papel
        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded) return result.ToApplicationResult();

        // 5. Lógica de Negócio: Se o papel atribuído for Administrador, promove a SuperUser
        if (roleName == Roles.Administrator && !user.IsSuperUser)
        {
            await SyncSuperUserStatusAsync(user);
        }

        return Result.Success();
    }

    public async Task<Result> RemoveRoleAsync(long userId, string roleName, CancellationToken ct)
    {
        // 1. Validações de existência (Defesa)
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Result.Failure(["Usuário não encontrado."]);

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null) return Result.Failure([$"Grupo '{roleName}' não encontrado."]);

        // 2. Performance: Se ele nem está no grupo, não precisamos fazer nada (Idempotência)
        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            return Result.Success();
        }

        // 3. Execução
        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded) return result.ToApplicationResult();

        // 4. Lógica de Negócio: Se removeu Admin, desativa a flag (Sincronia)
        if (roleName == Roles.Administrator && user.IsSuperUser)
        {
            await SyncSuperUserStatusAsync(user);
        }

        return Result.Success();
    }

    public async Task<bool> HasPermissionAsync(
        long userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var claims = await _userManager.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == CustomClaimTypes.Superuser && c.Value == "true"))
        {
            return true;
        }

        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);

        if (permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return Array.Empty<string>();
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var userClaims = await _userManager.GetClaimsAsync(user);

        foreach (var claim in userClaims.Where(c => c.Type == CustomClaimTypes.Permission))
        {
            permissions.Add(claim.Value);
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        foreach (var roleName in userRoles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);

            if (role is null)
            {
                continue;
            }

            var roleClaims = await _roleManager.GetClaimsAsync(role);

            foreach (var claim in roleClaims.Where(c => c.Type == CustomClaimTypes.Permission))
            {
                permissions.Add(claim.Value);
            }
        }

        return permissions.ToArray();
    }

    public async Task<string?> GetFullNameAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.FullName;
    }

    private async Task SyncSuperUserStatusAsync(ApplicationUser user)
    {
        // Verifica se o usuário possui a role de Administrador
        var isAdmin = await _userManager.IsInRoleAsync(user, Roles.Administrator);

        // Se a flag estiver diferente do status do grupo, atualiza
        if (user.IsSuperUser != isAdmin)
        {
            user.IsSuperUser = isAdmin;
            await _userManager.UpdateAsync(user);
        }
    }

    private static UserDto MapToUserDto(
        ApplicationUser user,
        IEnumerable<string> roles,
        string? createdByName = null,
        string? updatedByName = null)
    {
        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            CreatedByName = createdByName,
            UpdatedAt = user.UpdatedAt,
            UpdatedByName = updatedByName,
            UpdatedBy = user.UpdatedBy,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.ToArray()
        };
    }

    public async Task<bool> IsSuperUserAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user != null && user.IsSuperUser;
    }

    public async Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = userIds.Distinct().ToList();

        if (distinctIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var users = await _userManager.Users
            .AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                Name = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName
            })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => u.Name ?? u.Id.ToString());
    }
}
