namespace Atlas.Application.Platform.Users.Commands.DeactivateUser;

[Authorize(Policy = PermissionKeys.Users.Deactivate)]
public record DeactivateUserCommand : IRequest<Result>
{
    public long UserId { get; init; } = 0;
}

public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public DeactivateUserCommandHandler(
        IIdentityService identityService,
        IUser user)
    {
        _identityService = identityService;
        _user = user;
    }

    public async Task<Result> Handle(
        DeactivateUserCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == _user.Id)
        {
            throw new ValidationException("User", ["Operação inválida. Você não pode desativar a sua própria conta de usuário."]);
        }

        var result = await _identityService.DeactivateUserAsync(
            request.UserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new ValidationException("User", result.Errors ?? ["Falha ao inativar o usuário."]);
        }

        return result;
    }
}
