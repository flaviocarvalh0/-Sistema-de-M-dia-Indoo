namespace Atlas.Application.Platform.Permissions.Dtos;

/// <summary>
/// Representa uma permissão persistida em claim, enriquecida com o módulo e a entidade
/// aos quais a permissão está associada.
/// </summary>
public record PersistedPermissionClaimDto(
    string ClaimValue,
    long? ModuleId,
    string? EntityName);
