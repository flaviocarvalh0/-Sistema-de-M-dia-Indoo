namespace Atlas.Application.Platform.Users.Queries.GetUserById;

[Authorize(Policy = PermissionKeys.Users.View)]
public record GetUserByIdQuery : IRequest<UserDto?>
{
    public long UserId { get; init; }
}

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IIdentityService _identityService;

    public GetUserByIdQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<UserDto?> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _identityService.GetUserByIdAsync(
            request.UserId,
            cancellationToken);
    }
}
