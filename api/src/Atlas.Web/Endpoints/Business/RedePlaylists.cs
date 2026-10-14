using Atlas.Application.Business.Rede.Playlists.Commands.DefinirItensPlaylist;
using Atlas.Application.Business.Rede.Playlists.Commands.EstadoPlaylist;
using Atlas.Application.Business.Rede.Playlists.Commands.SalvarPlaylist;
using Atlas.Application.Business.Rede.Playlists.Dtos;
using Atlas.Application.Business.Rede.Playlists.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Playlists (grades) da rede, montadas pela operadora com as mídias dos
/// parceiros (anunciantes). O parceiro não decide onde o anúncio dele passa.
/// </summary>
public class RedePlaylists : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/playlists";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Playlists")
            .WithDescription("Grades de exibição da rede, reaproveitáveis entre telas.");

        groupBuilder.MapGet(GetPlaylists).RequireAuthorization(BusinessPermissionKeys.Playlists.View);
        groupBuilder.MapGet(GetPlaylistById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Playlists.View);
        groupBuilder.MapPost(CriarPlaylist).RequireAuthorization(BusinessPermissionKeys.Playlists.Create);
        groupBuilder.MapPut(AtualizarPlaylist, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Playlists.Update);
        groupBuilder.MapPut(DefinirItens, "{id:long}/itens").RequireAuthorization(BusinessPermissionKeys.Playlists.Update);
        groupBuilder.MapPost(AtivarPlaylist, "{id:long}/ativar").RequireAuthorization(BusinessPermissionKeys.Playlists.Update);
        groupBuilder.MapPost(InativarPlaylist, "{id:long}/inativar").RequireAuthorization(BusinessPermissionKeys.Playlists.Update);
        groupBuilder.MapDelete(ExcluirPlaylist, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Playlists.Delete);
    }

    [EndpointSummary("Listar playlists")]
    public static async Task<Ok<PaginatedList<PlaylistDto>>> GetPlaylists(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetPlaylistsQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            Ativo = ativo,
        }, cancellationToken));
    }

    [EndpointSummary("Obter playlist")]
    [EndpointDescription("Com os itens na ordem do loop.")]
    public static async Task<Ok<PlaylistDto>> GetPlaylistById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetPlaylistByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Criar playlist")]
    public static async Task<Created<long>> CriarPlaylist(
        ISender sender, CriarPlaylistCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar playlist")]
    [EndpointDescription("Nome, descrição e composição da fila (notícias, embaralhar, relógio e clima). Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> AtualizarPlaylist(
        ISender sender, long id, AtualizarPlaylistCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Definir itens da playlist")]
    [EndpointDescription("Recebe a grade inteira na ordem do loop (a posição na lista vira a ordem). Item com id atualiza, sem id entra, ausente sai. Devolve a nova versão.")]
    public static async Task<Ok<uint>> DefinirItens(
        ISender sender, long id, DefinirItensPlaylistCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Ativar playlist")]
    public static async Task<Ok<uint>> AtivarPlaylist(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new AtivarPlaylistCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Inativar playlist")]
    [EndpointDescription("As telas que a exibem passam a mostrar a tela de espera.")]
    public static async Task<Ok<uint>> InativarPlaylist(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new InativarPlaylistCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Excluir playlist")]
    [EndpointDescription("Recusada se alguma tela exibir a playlist. As mídias ficam.")]
    public static async Task<NoContent> ExcluirPlaylist(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirPlaylistCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}
