namespace Atlas.Application.Platform.Roles.Commands.UpdateRole;

[Authorize(Policy = PermissionKeys.Roles.Update)]
public record UpdateRoleCommand : IRequest<Result>
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IRoleService _roleService;
    private readonly IUser _currentUser;

    public UpdateRoleCommandHandler(IRoleService roleService, IUser currentUser)
    {
        _roleService = roleService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        UpdateRoleCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Id == SystemRoles.AdministratorsRoleId)
        {
            if (!string.Equals(request.Name, Atlas.Domain.Constants.Roles.Administrator, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(["Não é permitido renomear o grupo administrativo padrão do sistema."]);
            }

            if (!request.IsActive)
            {
                return Result.Failure(["O grupo administrativo padrão não pode ser desativado."]);
            }
        }

        var roleName = request.Name.Trim();

        var roleDto = await _roleService.GetRoleByIdAsync(request.Id, cancellationToken);

        if (roleDto is null)
        {
            return Result.Failure(["Grupo não encontrado para atualização."]);
        }

        if (!string.Equals(roleDto.Name, roleName, StringComparison.OrdinalIgnoreCase))
        {
            var nameExists = await _roleService.RoleExistsAsync(roleName, cancellationToken);

            if (nameExists)
            {
                return Result.Failure([$"O nome de grupo '{roleName}' já está sendo utilizado por outro cadastro."]);
            }
        }

        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("Usuário não identificado.");

        return await _roleService.UpdateRoleAsync(
            request.Id,
            roleName,
            request.IsActive,
            userId,
            cancellationToken);
    }
}