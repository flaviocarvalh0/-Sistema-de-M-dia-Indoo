namespace Atlas.Application.Platform.Users.Queries.GetUsers;

[Authorize(Policy = PermissionKeys.Users.View)]
public record GetUsersQuery : IRequest<IReadOnlyCollection<UserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IReadOnlyCollection<UserDto>>
{
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<IReadOnlyCollection<UserDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        return await _identityService.GetUsersAsync(cancellationToken);
    }
}
