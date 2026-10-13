using Atlas.Application.Platform.Roles.Dtos;

namespace Atlas.Application.Platform.Roles.Queries.GetRoles;

[Authorize(Policy = PermissionKeys.Roles.View)]
public record GetRolesQuery : IRequest<IReadOnlyCollection<RoleDto>>;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IReadOnlyCollection<RoleDto>>
{
    private readonly IRoleService _roleService;

    public GetRolesQueryHandler(IRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task<IReadOnlyCollection<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        return await _roleService.GetRolesAsync(cancellationToken);
    }
}
