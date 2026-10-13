namespace Atlas.Infrastructure.Identity;

/// <summary>
/// Representa uma claim de permissão associada a um grupo de acesso,
/// enriquecida com informações estruturadas de módulo e entidade.
/// </summary>
/// <remarks>
/// Além do valor tradicional da claim, armazena o módulo e a entidade relacionados,
/// permitindo montar matrizes administrativas de permissões sem depender apenas
/// da interpretação de strings.
/// </remarks>
public class ApplicationRoleClaim : IdentityRoleClaim<long>
{
    public long? ModuleId { get; set; }

    public string? EntityName { get; set; }
}