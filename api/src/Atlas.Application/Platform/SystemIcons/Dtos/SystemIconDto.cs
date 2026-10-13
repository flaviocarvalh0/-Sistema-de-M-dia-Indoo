namespace Atlas.Application.Platform.SystemIcons.Dtos;

public record SystemIconDto(
    long Id,
    string Name,
    string ContentType,
    string Extension,
    long SizeInBytes,
    DateTimeOffset Created,
    uint Version);
