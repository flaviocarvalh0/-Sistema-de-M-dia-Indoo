namespace Atlas.Application.Platform.EntityMetadata.Queries.GetEntityMetadata;

public record EntityMetadataDto(
    long Id,
    string TableName,
    string DisplayLabel,
    string RouteKey,
    bool EnableHistory,
    bool EnableExport,
    int DefaultPageSize,
    string? CodePrefix,
    uint Version
);
