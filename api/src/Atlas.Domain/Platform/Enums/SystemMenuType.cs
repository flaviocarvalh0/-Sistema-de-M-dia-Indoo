namespace Atlas.Domain.Platform.Enums;

/// <summary>
/// Define a estratégia de renderização e comportamento do item de menu.
/// </summary>
public enum SystemMenuType
{
    /// <summary>
    /// Item agrupador sem ação direta, utilizado para organizar submenus.
    /// </summary>
    Menu = 1,

    /// <summary>
    /// Item vinculado a uma entidade configurável, permitindo navegação para uma tela dinâmica baseada em metadados.
    /// </summary>
    Entity = 2
}
