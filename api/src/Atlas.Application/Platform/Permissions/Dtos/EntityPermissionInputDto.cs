namespace Atlas.Application.Platform.Permissions.Dtos;

/// <summary>
/// Objeto de transferência que representa as ações booleanas permitidas sobre uma tabela física do sistema.
/// </summary>
public record EntityPermissionInputDto
{
    /// <summary>
    /// Nome exato da tabela física no banco de dados (Ex: "Empresa", "SystemModule"). 
    /// Utilizado como chave de composição na geração das Claims.
    /// </summary>
    public string PhysicalTableName { get; init; } = null!;

    /// <summary>
    /// Indica se o grupo possui permissão de criação (POST).
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Indica se o grupo possui permissão de leitura/visualização (GET).
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Indica se o grupo possui permissão de edição/atualização (PUT/PATCH).
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Indica se o grupo possui permissão de remoção/inativação (DELETE).
    /// </summary>
    public bool CanDelete { get; set; }
}