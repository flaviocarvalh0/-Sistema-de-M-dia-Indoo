using Atlas.Application.Platform.EntityHistory.Common;
using Atlas.Application.Platform.EntityHistory.Queries.GetEntityHistory;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;

public class EntityHistory : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var historyGroup = groupBuilder.WithTags("Histórico de Alterações");

        historyGroup
            .MapGet("{routeKey}/{recordId:long}", GetEntityHistory)
            .RequireAuthorization();
    }

    [EndpointDescription(
        "Obtém o histórico operacional de versões de um registro, " +
        "com comparação de campos e paginação baseada em cursor.")]
    public static async Task<Ok<EntityHistoryDto>> GetEntityHistory(
        string routeKey,
        long recordId,
        long? beforeHistoryId,
        ISender sender,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEntityHistoryQuery
        {
            RouteKey = routeKey,
            RecordId = recordId,
            BeforeHistoryId = beforeHistoryId,
            PageSize = pageSize
        };

        var result = await sender.Send(query, cancellationToken);

        return TypedResults.Ok(result);
    }
}
