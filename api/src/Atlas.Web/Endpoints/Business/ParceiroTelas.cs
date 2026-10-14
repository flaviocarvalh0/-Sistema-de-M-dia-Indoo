using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// "Onde meus anúncios passam": as TVs cujas playlists têm alguma mídia ativa
/// do parceiro do usuário logado. Somente leitura — telas e playlists são da
/// operadora (<see cref="RedeTelas"/>, <see cref="RedePlaylists"/>).
/// </summary>
public class ParceiroTelas : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/parceiro/telas";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Parceiro: Telas")
            .WithDescription("TVs onde as mídias do parceiro do usuário logado estão passando.");

        groupBuilder.MapGet(GetTelas).RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapGet(GetTelaById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.View);
    }

    [EndpointSummary("Onde meus anúncios passam")]
    [EndpointDescription("Telas ativas cuja playlist tem alguma mídia ativa do parceiro, com o local e a situação (online/offline).")]
    public static async Task<Ok<PaginatedList<TelaDto>>> GetTelas(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelasQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
        }, cancellationToken));
    }

    [EndpointSummary("Obter tela")]
    public static async Task<Ok<TelaDto>> GetTelaById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelaByIdQuery(id), cancellationToken));
    }
}
