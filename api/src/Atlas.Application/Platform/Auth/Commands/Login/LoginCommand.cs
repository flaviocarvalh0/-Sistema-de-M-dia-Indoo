namespace Atlas.Application.Platform.Auth.Commands.Login;

public record LoginCommand : IRequest<Result<LoginResponse>>
{
    public required string UserName { get; init; }

    [LogSensitive]
    public required string Password { get; init; }
    public LoginMode Mode { get; init; } = LoginMode.Cookie;
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        return await _authService.LoginAsync(
            request.UserName,
            request.Password,
            request.Mode,
            cancellationToken);
    }
}
