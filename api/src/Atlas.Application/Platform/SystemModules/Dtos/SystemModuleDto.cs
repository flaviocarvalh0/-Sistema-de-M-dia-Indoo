namespace Atlas.Application.Platform.SystemModules.Dtos;

public record SystemModuleDto(
    long Id,
    string Name,
    string Description,
    string RoutePrefix,
    int SortOrder,
    long? SystemIconId,
    string HexColor,
    List<string> AssociatedEntities,
    uint Version);
