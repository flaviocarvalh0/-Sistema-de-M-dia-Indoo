namespace Atlas.Application.Platform.Roles.Commands.CreateRole;

[Authorize(Policy = PermissionKeys.Roles.Create)]
public record CreateRoleCommand : IRequest<Result<long>>
{
    public string Name { get; init; } = string.Empty;
}

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Result<long>>
{
    private readonly IRoleService _roleService;
    private readonly IUser _currentUser;

    public CreateRoleCommandHandler(IRoleService roleService, IUser currentUser)
    {
        _roleService = roleService;
        _currentUser = currentUser;
    }

    public async Task<Result<long>> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var roleName = request.Name.Trim();

        var roleExists = await _roleService.RoleExistsAsync(roleName, cancellationToken);

        if (roleExists)
        {
            return Result<long>.Failure([$"O grupo '{roleName}' já está cadastrado no sistema."]);
        }

        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Usuário não identificado.");

        return await _roleService.CreateRoleAsync(
            roleName,
            userId,
            cancellationToken);
    }
}