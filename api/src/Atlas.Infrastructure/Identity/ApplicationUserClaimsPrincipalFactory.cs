using Atlas.Domain.Constants;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Atlas.Infrastructure.Identity;

public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager, 
        RoleManager<ApplicationRole> roleManager, 
        IOptions<IdentityOptions> options) 
        : base(userManager, roleManager, options)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.IsSuperUser)
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Superuser, "true"));
        }

        // 1. Permissões diretas do usuário: AspNetUserClaims
        var userClaims = await _userManager.GetClaimsAsync(user);

        foreach (var claim in userClaims.Where(c => c.Type == CustomClaimTypes.Permission))
        {
            AddClaimIfMissing(identity, claim);
        }

        // 2. Permissões herdadas dos grupos/roles: AspNetRoleClaims
        var roleNames = await _userManager.GetRolesAsync(user);

        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);

            if (role is null)
            {
                continue;
            }

            var roleClaims = await _roleManager.GetClaimsAsync(role);

            foreach (var claim in roleClaims.Where(c => c.Type == CustomClaimTypes.Permission))
            {
                AddClaimIfMissing(identity, claim);
            }
        }

        AddClaimIfMissing(identity, new Claim("is_active", user.IsActive.ToString().ToLowerInvariant()));
        AddClaimIfMissing(identity, new Claim("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant()));

        return identity;
    }

    private static void AddClaimIfMissing(ClaimsIdentity identity, Claim claim)
    {
        if (!identity.HasClaim(claim.Type, claim.Value))
        {
            identity.AddClaim(claim);
        }
    }
}
