namespace Atlas.Application.Platform.SystemIcons.Dtos;

public record SystemIconFileDto(
    [property: LogIgnore] byte[] Data,
    string ContentType);
