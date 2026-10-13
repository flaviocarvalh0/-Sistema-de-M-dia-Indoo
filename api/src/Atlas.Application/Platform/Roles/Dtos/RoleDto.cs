namespace Atlas.Application.Platform.Roles.Dtos;

/// <summary>
/// Representa um grupo/perfil de acesso cadastrado na plataforma.
/// </summary>
public record RoleDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
}
