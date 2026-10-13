namespace Atlas.Infrastructure.Identity;

/// <summary>
/// Representa uma claim de permissão associada diretamente a um usuário,
/// enriquecida com informações estruturadas de módulo e entidade.
/// </summary>
/// <remarks>
/// Deve ser utilizada para permissões diretas do usuário, independentes das permissões herdadas
/// por meio dos grupos de acesso.
/// </remarks>
public class ApplicationUserClaim : IdentityUserClaim<long>
{
    public long? ModuleId { get; set; }

    public string? EntityName { get; set; }
}