using System.Text.Json.Serialization;

namespace Atlas.Application.Platform.Users.Commands.AssignUserRole;

[Authorize(Policy = PermissionKeys.Users.AssignRole)]
public record AssignUserRoleCommand : IRequest<Result>
{
    [JsonIgnore]
    public long UserId { get; init; }

    public string RoleName { get; init; } = string.Empty;
}

public class AssignUserRoleCommandHandler : IRequestHandler<AssignUserRoleCommand, Result>
{
    private readonly IIdentityService _identityService;

    public AssignUserRoleCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(
        AssignUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        return await _identityService.AssignRoleAsync(
            request.UserId,
            request.RoleName,
            cancellationToken);
    }
}
