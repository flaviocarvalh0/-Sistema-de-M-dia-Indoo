namespace Atlas.Application.Platform.Roles.Dtos;

public record PermissionItemDto
{
    public string Value { get; init; } = string.Empty;   
    public string Description { get; init; } = string.Empty;
    public bool IsGranted { get; init; }  
}
