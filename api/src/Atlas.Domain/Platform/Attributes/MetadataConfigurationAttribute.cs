namespace Atlas.Domain.Platform.Attributes;

/// <summary>
/// Indica que uma entidade deve possuir metadados gerenciáveis pela plataforma.
/// </summary>
/// <remarks>
/// Entidades marcadas com este atributo podem ser registradas automaticamente
/// na tabela de metadados do sistema, permitindo configuração dinâmica de rótulo,
/// rota, ícone e comportamentos de exibição.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MetadataConfigurationAttribute : Attribute
{
    public MetadataConfigurationAttribute(string displayLabel)
    {
        DisplayLabel = displayLabel;
    }

    /// <summary>
    /// Rótulo amigável padrão da entidade.
    /// </summary>
    public string DisplayLabel { get; }

    /// <summary>
    /// Chave de rota opcional. Quando não informada, será gerada automaticamente.
    /// </summary>
    public string? RouteKey { get; init; }

    /// <summary>
    /// Tamanho padrão de página para listagens da entidade.
    /// </summary>
    public int DefaultPageSize { get; init; } = 100;

    /// <summary>
    /// Indica se a entidade deve disponibilizar histórico por padrão.
    /// </summary>
    public bool EnableHistory { get; init; } = true;

    /// <summary>
    /// Indica se a entidade deve permitir exportação por padrão.
    /// </summary>
    public bool EnableExport { get; init; } = true;

    /// <summary>
    /// Prefixo opcional para códigos associados à entidade.
    /// </summary>
    public string? CodePrefix { get; init; }
}