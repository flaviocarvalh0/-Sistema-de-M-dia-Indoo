using Atlas.Application.Platform.EntityHistory.Common;

namespace Atlas.Application.Common.Interfaces;
public interface IEntityHistoryQueryService
{
    Task<EntityHistoryDto> GetAsync(
        string routeKey,
        long recordId,
        long? beforeHistoryId = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default);
}
