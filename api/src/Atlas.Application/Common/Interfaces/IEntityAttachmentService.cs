using Atlas.Application.Platform.EntityAttachments.Common;

namespace Atlas.Application.Common.Interfaces;

public interface IEntityAttachmentService
{
    Task<EntityAttachmentDto> CreateAsync(
        CreateEntityAttachmentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EntityAttachmentDto>> GetAsync(
        string routeKey,
        long recordId,
        CancellationToken cancellationToken = default);

    Task<EntityAttachmentContentDto> GetContentAsync(
        long attachmentId,
        CancellationToken cancellationToken = default);

    Task<EntityAttachmentDto> UpdateDetailsAsync(
        long attachmentId,
        uint version,
        string? title,
        string? description,
        DateTimeOffset? referenceDate,
        CancellationToken cancellationToken = default);

    Task<DeleteEntityAttachmentResponse> DeleteAsync(
        long attachmentId,
        uint version,
        CancellationToken cancellationToken = default);
}
