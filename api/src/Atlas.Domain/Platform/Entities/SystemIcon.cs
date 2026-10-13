namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa um ícone reutilizável da plataforma, armazenado em banco de dados
/// para associação com menus, módulos, entidades e demais recursos visuais do sistema.
/// </summary>
public class SystemIcon : BaseAuditableEntity
{
    /// <summary>
    /// Nome amigável do ícone no catálogo da plataforma.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo MIME do arquivo do ícone. Exemplo: image/svg+xml, image/png.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Extensão do arquivo original. Exemplo: svg, png, webp.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do arquivo em bytes.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Conteúdo binário do arquivo do ícone.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public bool IsSystemDefined { get; set; } = false;
}

