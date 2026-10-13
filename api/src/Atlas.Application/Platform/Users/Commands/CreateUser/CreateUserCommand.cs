namespace Atlas.Application.Platform.Users.Commands.CreateUser;

[Authorize(Policy = PermissionKeys.Users.Create)]
public record CreateUserCommand : IRequest<long>
{
    public string FullName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;

    [LogSensitive]
    public string? Email { get; init; }

    [LogSensitive]
    public string TemporaryPassword { get; init; } = string.Empty;
    public bool MustChangePassword { get; init; } = true;
    public IList<string> Roles { get; init; } = [];
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, long>
{
    private readonly IIdentityService _identityService;

    public CreateUserCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<long> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserAsync(
            request.FullName,
            request.UserName,
            request.Email,
            request.TemporaryPassword,
            request.MustChangePassword,
            request.Roles,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new ValidationException("Identity", result.Errors ?? ["Erro desconhecido ao criar usuário."]); ;
        }

        return result.Value!;
    }
}