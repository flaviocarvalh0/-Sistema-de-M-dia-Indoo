namespace Atlas.Infrastructure.Identity;

/// <summary>
/// Representa um grupo/perfil de acesso da plataforma, estendendo a role padrão do ASP.NET Identity
/// com status de ativação e metadados de auditoria.
/// </summary>
/// <remarks>
/// Os grupos são utilizados para organizar permissões e controlar o acesso dos usuários aos recursos da plataforma.
/// </remarks>
public class ApplicationRole : IdentityRole<long>
{
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public ApplicationRole() : base()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
        Name = roleName;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}