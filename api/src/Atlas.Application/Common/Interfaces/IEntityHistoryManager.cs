namespace Atlas.Application.Common.Interfaces;
public interface IEntityHistoryManager
{
    Task EnsureEnabledHistoriesAsync(
        CancellationToken cancellationToken = default);

    Task EnableAsync(
        long entityMetadataId,
        CancellationToken cancellationToken = default);

    Task DisableAsync(
        long entityMetadataId,
        CancellationToken cancellationToken = default);
}
