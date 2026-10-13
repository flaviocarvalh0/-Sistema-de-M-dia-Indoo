namespace Atlas.Application.Platform.Roles.Dtos;

public record PermissionCategoryDto
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyCollection<PermissionItemDto> Permissions { get; init; } = [];
}
