namespace Atlas.Domain.Platform.Entities;


/// <summary>
/// Representa um módulo funcional da plataforma, utilizado para agrupar entidades,
/// definir rotas base, ordenação e identidade visual no front-end.
/// </summary>
/// <remarks>
/// Um módulo pode ser utilizado para organizar menus, áreas do sistema e recursos configuráveis,
/// permitindo que entidades descobertas pela plataforma sejam agrupadas de forma dinâmica.
/// </remarks>
public class SystemModule : BaseAuditableEntity
{
    /// <summary>
    /// Nome amigável do módulo exibido na interface.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição resumida da finalidade do módulo.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Prefixo de rota utilizado pelo front-end para organizar a navegação do módulo.
    /// Exemplo: "/rh", "/financeiro", "/estoque".
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Ordem de exibição do módulo em menus e listagens.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Identificador do ícone associado ao módulo, quando houver.
    /// </summary>
    public long? SystemIconId { get; set; }

    /// <summary>
    /// Ícone associado ao módulo para exibição visual no front-end.
    /// </summary>
    public SystemIcon? SystemIcon { get; set; }

    /// <summary>
    /// Cor em formato hexadecimal (ex: "#FF0000") para identidade visual do módulo.
    /// </summary>
    public string HexColor { get; set; } = string.Empty;

    /// <summary>
    /// Entidades/metadados associados a este módulo.
    /// </summary>
    public virtual ICollection<EntityMetadata> Entities { get; set; } = new HashSet<EntityMetadata>();
}
