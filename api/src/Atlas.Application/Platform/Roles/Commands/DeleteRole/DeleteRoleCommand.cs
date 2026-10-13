namespace Atlas.Application.Platform.Roles.Commands.DeleteRole;

[Authorize(Policy = PermissionKeys.Roles.Delete)]
public record DeleteRoleCommand(long Id) : IRequest<Result>;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result>
{
    private readonly IRoleService _roleService;

    public DeleteRoleCommandHandler(IRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == SystemRoles.AdministratorsRoleId)
        {
            return Result.Failure(["O grupo de Administradores é essencial para a integridade do sistema e não pode ser removido."]);
        }

        var role = await _roleService.GetRoleByIdAsync(request.Id, cancellationToken);
        if (role is null)
        {
            return Result.Failure(["Grupo não encontrado."]);
        }

        return await _roleService.DeleteRoleAsync(request.Id, cancellationToken);
    }
}