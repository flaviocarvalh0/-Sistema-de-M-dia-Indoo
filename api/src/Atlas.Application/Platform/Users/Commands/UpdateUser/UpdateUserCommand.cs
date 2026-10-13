using System.Text.Json.Serialization;

namespace Atlas.Application.Platform.Users.Commands.UpdateUser;

[Authorize(Policy = PermissionKeys.Users.Update)]
public record UpdateUserCommand : IRequest<Result>
{
    [JsonIgnore]
    public long UserId { get; init; }
    public string FullName { get; init; } = string.Empty;

    [LogSensitive]
    public string? Email { get; init; }
}

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public UpdateUserCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        return await _identityService.UpdateUserAsync(
            request.UserId,
            request.FullName,
            request.Email,
            cancellationToken);
    }
}
