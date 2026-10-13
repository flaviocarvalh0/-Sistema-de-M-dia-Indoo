namespace Atlas.Application.Platform.Permissions.Dtos;

public class UserPermissionsDto
{
    public long UserId { get; set; }
    public string? TargetName { get; set; }
    public List<ModulePermissionDto> Modules { get; set; } = [];
}
