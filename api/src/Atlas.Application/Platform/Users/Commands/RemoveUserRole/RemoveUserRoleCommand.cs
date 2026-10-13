namespace Atlas.Application.Platform.Users.Commands.RemoveUserRole;

[Authorize(Policy = PermissionKeys.Users.RemoveRole)]
public record RemoveUserRoleCommand : IRequest<Result>
{
    public long UserId { get; init; }

    public string RoleName { get; init; } = string.Empty;
}

public class RemoveUserRoleCommandHandler : IRequestHandler<RemoveUserRoleCommand, Result>
{
    private readonly IIdentityService _identityService;

    public RemoveUserRoleCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(
        RemoveUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        return await _identityService.RemoveRoleAsync(
            request.UserId,
            request.RoleName,
            cancellationToken);
    }
}
