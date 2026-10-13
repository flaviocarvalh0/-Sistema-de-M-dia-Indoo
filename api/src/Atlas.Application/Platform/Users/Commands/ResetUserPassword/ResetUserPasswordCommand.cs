using System.Text.Json.Serialization;

namespace Atlas.Application.Platform.Users.Commands.ResetUserPassword;

[Authorize(Policy = PermissionKeys.Users.ResetPassword)]
public record ResetUserPasswordCommand : IRequest<Result>
{
    [JsonIgnore]
    public long UserId { get; init; }

    [LogSensitive]
    public string TemporaryPassword { get; init; } = string.Empty;

    [LogSensitive]
    public string ConfirmTemporaryPassword { get; init; } = string.Empty;

    public bool MustChangePassword { get; init; } = true;
}

public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, Result>
{
    private readonly IIdentityService _identityService;

    public ResetUserPasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(
        ResetUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        return await _identityService.ResetPasswordAsync(
            request.UserId,
            request.TemporaryPassword,
            request.MustChangePassword,
            cancellationToken);
    }
}
