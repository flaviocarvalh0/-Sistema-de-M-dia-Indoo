using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa um arquivo anexado a um registro de qualquer entidade
/// conhecida pela plataforma.
/// </summary>
[MetadataConfiguration(
    "Anexos",
    EnableHistory = true)]
public class EntityAttachment : BaseAuditableEntity
{
    private EntityAttachment()
    {
    }

    public EntityAttachment(
        long entityMetadataId,
        long recordId,
        FileContent fileContent,
        string? title = null,
        string? description = null,
        DateTimeOffset? referenceDate = null)
    {
        if (entityMetadataId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entityMetadataId));
        }

        if (recordId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordId));
        }

        ArgumentNullException.ThrowIfNull(fileContent);

        EntityMetadataId = entityMetadataId;
        RecordId = recordId;
        FileContent = fileContent;

        SetDetails(
            title,
            description,
            referenceDate);
    }

    public long EntityMetadataId { get; private set; }
    public long RecordId { get; private set; }
    public long FileContentId { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset? ReferenceDate { get; private set; }
    public EntityMetadata EntityMetadata { get; private set; } = null!;
    public FileContent FileContent { get; private set; } = null!;

    public void UpdateDetails(
        string? title,
        string? description,
        DateTimeOffset? referenceDate)
    {
        SetDetails(
            title,
            description,
            referenceDate);
    }

    private void SetDetails(
        string? title,
        string? description,
        DateTimeOffset? referenceDate)
    {
        Title = NormalizeOptional(title);
        Description = NormalizeOptional(description);
        ReferenceDate = referenceDate;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
