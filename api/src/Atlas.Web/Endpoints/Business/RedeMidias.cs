using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Rede.Midias.Commands;
using Atlas.Application.Business.Rede.Midias.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// As mídias de todos os clientes, somente leitura, para a operadora montar
/// as grades (o criativo é do anunciante: ele envia, edita e ativa).
/// </summary>
public class RedeMidias : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/midias";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Mídias")
            .WithDescription("Mídias de todos os clientes, como a operadora as vê.");

        // Para montar grades: Playlists.View e Clientes.View, que só a operadora tem.
        groupBuilder.MapGet(GetMidiasDaRede)
            .RequireAuthorization(BusinessPermissionKeys.Playlists.View)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);
        groupBuilder.MapGet(GetMidiaDaRedeById, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Playlists.View)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);
        groupBuilder.MapPost(AprovarTrocaArquivo, "{id:long}/aprovar-troca")
            .RequireAuthorization(BusinessPermissionKeys.Playlists.Update)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);
        groupBuilder.MapPost(RecusarTrocaArquivo, "{id:long}/recusar-troca")
            .RequireAuthorization(BusinessPermissionKeys.Playlists.Update)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);
    }

    [EndpointSummary("Listar mídias da rede")]
    [EndpointDescription("Todas as mídias, com o cliente. Filtros por cliente, situação e tipo; busca por título ou cliente.")]
    public static async Task<Ok<PaginatedList<MidiaDto>>> GetMidiasDaRede(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] long? clienteId,
        [FromQuery] bool? ativo,
        [FromQuery] TipoMidia? tipo,
        [FromQuery] bool? trocaPendente,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiasRedeQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            ClienteId = clienteId,
            Ativo = ativo,
            Tipo = tipo,
            TrocaPendente = trocaPendente,
        }, cancellationToken));
    }

    [EndpointSummary("Obter mídia")]
    [EndpointDescription("Com o endereço temporário (1 h) para pré-visualizar.")]
    public static async Task<Ok<MidiaDto>> GetMidiaDaRedeById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiaRedeByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Aprovar troca de arquivo")]
    [EndpointDescription("O arquivo novo enviado pelo anunciante passa a valer; as TVs baixam na próxima sincronização. Devolve a nova versão.")]
    public static async Task<Ok<uint>> AprovarTrocaArquivo(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new AprovarTrocaArquivoCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Recusar troca de arquivo")]
    [EndpointDescription("O arquivo atual segue no ar; o anunciante recebe o motivo. Devolve a nova versão.")]
    public static async Task<Ok<uint>> RecusarTrocaArquivo(
        ISender sender, long id, RecusarTrocaArquivoCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }
}
