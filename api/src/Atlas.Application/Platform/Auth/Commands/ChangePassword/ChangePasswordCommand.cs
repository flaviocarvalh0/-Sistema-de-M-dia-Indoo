namespace Atlas.Application.Platform.Auth.Commands.ChangePassword;

[Authorize]
public record ChangePasswordCommand : IRequest<Result>
{
    [LogSensitive]
    public string CurrentPassword { get; init; } = string.Empty;

    [LogSensitive]
    public string NewPassword { get; init; } = string.Empty;

    [LogSensitive]
    public string ConfirmNewPassword { get; init; } = string.Empty;
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUser _user;
    private readonly IAuthService _authService;
    public ChangePasswordCommandHandler(IUser user, IAuthService authService)
    {
        _user = user;
        _authService = authService;
    }

    public async Task<Result> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!_user.Id.HasValue || _user.Id.Value <= 0)
        {
            throw new UnauthorizedAccessException();
        }

        return await _authService.ChangePasswordAsync(
            _user.Id.Value,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
    }
}
