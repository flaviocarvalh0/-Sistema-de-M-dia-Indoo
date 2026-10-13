namespace Atlas.Application.Platform.SystemModules.Queries.GetUserModules;

public record UserModuleDto
(
    long Id,
    string Name,
    string Description,
    string RoutePrefix,
    int SortOrder,
    long? SystemIconId,
    string HexColor
);
