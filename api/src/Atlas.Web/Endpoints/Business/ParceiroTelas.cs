using Atlas.Application.Business.Indoor.Telas.Commands.DefinirPlaylist;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Telas (TVs) do parceiro do usuário logado. O hardware é da operadora
/// (<see cref="RedeTelas"/>): aqui o parceiro só consulta as TVs dele e
/// escolhe a playlist de cada uma.
/// </summary>
public class ParceiroTelas : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/parceiro/telas";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Parceiro: Telas")
            .WithDescription("TVs do parceiro do usuário logado (somente leitura, mais a playlist exibida).");

        groupBuilder.MapGet(GetTelas).RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapGet(GetTelaById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapPut(DefinirPlaylist, "{id:long}/playlist")
            .RequireAuthorization(BusinessPermissionKeys.Telas.View)
            .RequireAuthorization(BusinessPermissionKeys.Playlists.Update);
    }

    [EndpointSummary("Listar telas")]
    [EndpointDescription("Com a situação (aguardando pareamento, online, offline) e a última sincronização.")]
    public static async Task<Ok<PaginatedList<TelaDto>>> GetTelas(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] bool? ativo,
        [FromQuery] long? playlistId,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelasQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            Ativo = ativo,
            PlaylistId = playlistId,
        }, cancellationToken));
    }

    [EndpointSummary("Obter tela")]
    public static async Task<Ok<TelaDto>> GetTelaById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelaByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Definir a playlist da tela")]
    [EndpointDescription("A playlist que a TV exibe (vale na próxima sincronização). Nula = tela de espera. Devolve a nova versão.")]
    public static async Task<Ok<uint>> DefinirPlaylist(
        ISender sender, long id, DefinirPlaylistDaTelaCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }
}
