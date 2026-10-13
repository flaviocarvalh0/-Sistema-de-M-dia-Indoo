namespace Atlas.Application.Platform.SystemMenus.Dtos;

public record SystemMenuItemDto(
    long Id,
    string DisplayLabel,
    int SortOrder,
    string MenuType,
    long? ParentId,
    long? SystemIconId,
    string? IconHexColor,
    long? EntityMetadataId,
    string? TargetRouteKey,
    uint Version,
    List<SystemMenuItemDto> Children);
