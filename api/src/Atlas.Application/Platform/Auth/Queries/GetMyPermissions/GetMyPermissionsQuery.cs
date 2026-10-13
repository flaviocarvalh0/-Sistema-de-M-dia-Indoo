using Atlas.Application.Platform.Permissions.Dtos;
using Atlas.Application.Platform.Permissions.Queries.GetUserPermissions;

namespace Atlas.Application.Platform.Auth.Queries.GetMyPermissions;

/// <summary>
/// Consulta responsável por recuperar a árvore de permissões do usuário atualmente autenticado na sessão.
/// </summary>
public record GetMyPermissionsQuery : IRequest<UserPermissionsDto?>;

public class GetMyPermissionsQueryHandler : IRequestHandler<GetMyPermissionsQuery, UserPermissionsDto?>
{
    private readonly IUser _user;
    private readonly ISender _sender;

    public GetMyPermissionsQueryHandler(IUser user, ISender sender)
    {
        _user = user;
        _sender = sender;
    }

    public async Task<UserPermissionsDto?> Handle(
        GetMyPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_user.Id.HasValue || _user.Id.Value <= 0)
        {
            throw new UnauthorizedAccessException();
        }

        return await _sender.Send(new GetUserPermissionsQuery
        {
            UserId = _user.Id.Value
        }, cancellationToken);
    }
}
