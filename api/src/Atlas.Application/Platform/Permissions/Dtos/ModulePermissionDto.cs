namespace Atlas.Application.Platform.Permissions.Dtos;

/// <summary>
/// Representa um módulo dentro da matriz de permissões.
/// </summary>
/// <remarks>
/// Agrupa as entidades configuráveis do módulo e indica se o módulo está habilitado
/// para o usuário ou grupo sendo editado.
/// </remarks>
public class ModulePermissionDto
{
    public long ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public bool IsModuleEnabled { get; set; }

    public List<EntityPermissionDto> Entities { get; set; } = [];
}
