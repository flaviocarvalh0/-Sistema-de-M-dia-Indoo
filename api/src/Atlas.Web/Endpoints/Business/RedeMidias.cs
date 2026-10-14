using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Rede.Midias.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// As mídias de todos os parceiros, somente leitura, para a operadora montar
/// as grades (o criativo é do anunciante: ele envia, edita e ativa).
/// </summary>
public class RedeMidias : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/midias";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Mídias")
            .WithDescription("Mídias de todos os parceiros, como a operadora as vê.");

        // Para montar grades: Playlists.View e Parceiros.View, que só a operadora tem.
        groupBuilder.MapGet(GetMidiasDaRede)
            .RequireAuthorization(BusinessPermissionKeys.Playlists.View)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapGet(GetMidiaDaRedeById, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Playlists.View)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
    }

    [EndpointSummary("Listar mídias da rede")]
    [EndpointDescription("Todas as mídias, com o parceiro. Filtros por parceiro, situação e tipo; busca por título ou parceiro.")]
    public static async Task<Ok<PaginatedList<MidiaDto>>> GetMidiasDaRede(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] long? parceiroId,
        [FromQuery] bool? ativo,
        [FromQuery] TipoMidia? tipo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiasRedeQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            ParceiroId = parceiroId,
            Ativo = ativo,
            Tipo = tipo,
        }, cancellationToken));
    }

    [EndpointSummary("Obter mídia")]
    [EndpointDescription("Com o endereço temporário (1 h) para pré-visualizar.")]
    public static async Task<Ok<MidiaDto>> GetMidiaDaRedeById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiaRedeByIdQuery(id), cancellationToken));
    }
}
