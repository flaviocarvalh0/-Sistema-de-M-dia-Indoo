namespace Atlas.Application.Platform.Permissions.Dtos;

/// <summary>
/// Representa uma entidade configurável dentro da matriz de permissões.
/// </summary>
/// <remarks>
/// Contém os dados exibidos no grid de permissões e os estados atuais das ações
/// que podem ser concedidas ao usuário ou grupo.
/// </remarks>
public class EntityPermissionDto
{
    /// <summary>
    /// Identificador dos metadados da entidade.
    /// </summary>
    public long EntityMetadataId { get; set; }

    /// <summary>
    /// Rótulo amigável exibido na interface.
    /// </summary>
    public string DisplayLabel { get; set; } = null!;

    /// <summary>
    /// Nome físico/técnico da tabela associada à entidade.
    /// </summary>
    public string PhysicalTableName { get; set; } = null!;

    /// <summary>
    /// Prefixo utilizado na composição das permissões técnicas.
    /// </summary>
    public string? PermissionPrefix { get; set; }

    /// <summary>
    /// Indica se a entidade está habilitada na matriz de permissões.
    /// </summary>
    public bool IsEntityEnabled { get; set; }

    public bool CanCreate { get; set; }
    public bool CanView { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}