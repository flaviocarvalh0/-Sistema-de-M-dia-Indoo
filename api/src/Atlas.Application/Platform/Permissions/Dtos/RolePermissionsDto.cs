namespace Atlas.Application.Platform.Permissions.Dtos;

public class RolePermissionsDto
{
    public long RoleId { get; set; }
    public string? TargetName { get; set; }
    public List<ModulePermissionDto> Modules { get; set; } = [];
}
