using System.ComponentModel.DataAnnotations;
using Atlas.Domain.Platform.Enums;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa um item da árvore de navegação da plataforma, podendo atuar como agrupador
/// de menus ou como entrada vinculada a uma entidade configurável.
/// </summary>
/// <remarks>
/// Os itens de menu são organizados por módulo e podem formar estruturas hierárquicas
/// por meio do relacionamento pai/filho. Quando vinculados a metadados de entidade,
/// permitem que o front-end renderize telas dinâmicas de navegação e CRUD.
/// </remarks>
public class SystemMenuItem : BaseAuditableEntity
{
    /// <summary>
    /// Rótulo exibido para o item de menu na interface.
    /// </summary>
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>
    /// Ordem de exibição do item dentro do mesmo nível da árvore.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Tipo do item de menu, definindo se ele atua como agrupador ou como item vinculado a uma entidade.
    /// </summary>
    public SystemMenuType MenuType { get; set; }

    /// <summary>
    /// Identificador do módulo ao qual este item de menu pertence.
    /// </summary>
    public long SystemModuleId { get; set; }

    /// <summary>
    /// Módulo ao qual este item de menu pertence.
    /// </summary>
    public SystemModule? SystemModule { get; set; }

    /// <summary>
    /// Identificador do item pai, quando este item estiver aninhado em outro menu.
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// Item de menu pai.
    /// </summary>
    public SystemMenuItem? Parent { get; set; }

    /// <summary>
    /// Itens filhos deste item de menu.
    /// </summary>
    public ICollection<SystemMenuItem> Children { get; set; } = new List<SystemMenuItem>();

    /// <summary>
    /// Identificador do ícone associado ao item de menu, quando houver.
    /// </summary>
    public long? SystemIconId { get; set; }

    /// <summary>
    /// Ícone associado ao item de menu.
    /// </summary>
    public SystemIcon? SystemIcon { get; set; }

    /// <summary>
    /// Identificador dos metadados de entidade associados ao item, quando o tipo for Entity.
    /// </summary>
    public long? EntityMetadataId { get; set; }

    /// <summary>
    /// Metadados da entidade associada ao item de menu.
    /// </summary>
    public EntityMetadata? EntityMetadata { get; set; }

    /// <summary>
    /// Cor em formato hexadecimal (ex: #FF5733) aplicada ao ícone. 
    /// Se nulo, o ícone deve ser renderizado com a cor original definida no arquivo.
    /// </summary>
    [MaxLength(7)] // Formato #RRGGBB
    public string? IconHexColor { get; set; }
}
