namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa os metadados configuráveis de uma entidade/recurso da plataforma,
/// permitindo controlar informações de exibição e comportamento sem necessidade de recompilação.
/// </summary>
/// <remarks>
/// Esses metadados podem ser utilizados pelo front-end para montar menus, rótulos amigáveis,
/// ícones, paginação padrão, histórico, exportação e outras capacidades dinâmicas associadas a uma entidade.
/// </remarks>
public class EntityMetadata : BaseAuditableEntity
{
    /// <summary>
    /// Nome técnico da tabela ou entidade associada ao metadado.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Rótulo amigável exibido para o usuário no front-end.
    /// </summary>
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>
    /// Chave de rota utilizada pelo front-end para navegação ou identificação do recurso.
    /// </summary>
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>
    /// Identificador do ícone associado à entidade, quando houver.
    /// </summary>
    public long? SystemIconId { get; set; }

    /// <summary>
    /// Ícone associado à entidade, utilizado para exibição em menus e componentes visuais.
    /// </summary>
    public SystemIcon? SystemIcon { get; set; }

    /// <summary>
    /// Quantidade padrão de registros exibidos por página em listagens dessa entidade.
    /// </summary>
    public int DefaultPageSize { get; set; } = 100;

    /// <summary>
    /// Indica se a entidade deve disponibilizar histórico/auditoria para consulta.
    /// </summary>
    public bool EnableHistory { get; set; } = true;

    /// <summary>
    /// Indica se a entidade permite exportação de dados.
    /// </summary>
    public bool EnableExport { get; set; } = true;

    /// <summary>
    /// Prefixo opcional utilizado na geração ou exibição de códigos associados à entidade.
    /// </summary>
    public string? CodePrefix { get; set; }

    /// <summary>
    /// Identificador do módulo do sistema ao qual esta entidade foi associada pelo administrador.
    /// Pode ser nulo caso a entidade tenha sido recém-descoberta e ainda não vinculada a um módulo.
    /// </summary>
    public long? SystemModuleId { get; set; }

    /// <summary>
    /// Módulo do sistema proprietário desta entidade, responsável por agrupar suas permissões, 
    /// identidade visual e posicionamento no ecossistema.
    /// </summary>
    public SystemModule? SystemModule { get; set; }
}