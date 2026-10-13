namespace Atlas.Application.Platform.Auth.Queries.GetCurrentUser;

[Authorize]
public record GetCurrentUserQuery : IRequest<UserDto?>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto?>
{
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(IUser user, IIdentityService identityService)
    {
        _user = user;
        _identityService = identityService;
    }

    public async Task<UserDto?> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        if (!_user.Id.HasValue || _user.Id.Value <=0)
        {
            throw new UnauthorizedAccessException();
        }

        return await _identityService.GetUserByIdAsync(
            _user.Id.Value,
            cancellationToken);
    }
}
