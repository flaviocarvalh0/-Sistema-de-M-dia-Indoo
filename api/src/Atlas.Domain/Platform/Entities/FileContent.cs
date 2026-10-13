using Atlas.Shared.Observability;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa um conteúdo binário persistido pela plataforma.
///
/// O conteúdo é imutável após sua criação e pode ser reutilizado
/// por diferentes recursos da plataforma.
/// </summary>
public class FileContent : BaseAuditableEntity
{
    private FileContent()
    {
    }

    public FileContent(
        string fileName,
        string contentType,
        string extension,
        string hash,
        byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new ArgumentException(
                "O conteúdo do arquivo não pode estar vazio.",
                nameof(content));
        }

        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        Extension = NormalizeExtension(extension);
        Hash = hash.Trim();
        Content = content.ToArray();
        Size = content.LongLength;
    }

    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string Extension { get; private set; } = null!;
    public long Size { get; private set; }
    public string Hash { get; private set; } = null!;

    [LogIgnore]
    public byte[] Content { get; private set; } = null!;

    private static string NormalizeExtension(
        string extension)
    {
        var normalized = extension.Trim();

        if (!normalized.StartsWith('.'))
        {
            normalized = $".{normalized}";
        }

        return normalized.ToLowerInvariant();
    }
}
