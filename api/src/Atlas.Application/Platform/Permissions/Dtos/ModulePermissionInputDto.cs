namespace Atlas.Application.Platform.Permissions.Dtos;

/// <summary>
/// Objeto de transferência de dados contendo os metadados do módulo para entrada de permissões.
/// </summary>
public record ModulePermissionInputDto
{
    /// <summary>
    /// Identificador único do módulo do sistema.
    /// </summary>
    public long ModuleId { get; init; }

    /// <summary>
    /// Lista de entidades pertencentes a este módulo com seus respectivos estados de permissão.
    /// </summary>
    public List<EntityPermissionInputDto> Entities { get; init; } = [];
}