namespace Atlas.Application.Platform.Users.Commands.ActivateUser;

[Authorize(Policy = PermissionKeys.Users.Activate)]
public record ActivateUserCommand : IRequest<Result>
{
    public long UserId { get; init; }
}

public class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public ActivateUserCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(
        ActivateUserCommand request,
        CancellationToken cancellationToken)
    {
        return await _identityService.ActivateUserAsync(
            request.UserId,
            cancellationToken);
    }
}
