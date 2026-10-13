using Atlas.Application.Business.Indoor.Telas.Commands.EstadoTela;
using Atlas.Application.Business.Indoor.Telas.Commands.Pareamento;
using Atlas.Application.Business.Indoor.Telas.Commands.SalvarTela;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Telas (TVs) do parceiro do usuário logado: cadastro, playlist exibida e pareamento.</summary>
public class ParceiroTelas : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/parceiro/telas";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Parceiro: Telas")
            .WithDescription("TVs do parceiro do usuário logado.");

        groupBuilder.MapGet(GetTelas).RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapGet(GetTelaById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapPost(CriarTela).RequireAuthorization(BusinessPermissionKeys.Telas.Create);
        groupBuilder.MapPut(AtualizarTela, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(GerarCodigoPareamento, "{id:long}/codigo-pareamento").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(DesvincularTela, "{id:long}/desvincular").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(AtivarTela, "{id:long}/ativar").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(InativarTela, "{id:long}/inativar").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapDelete(ExcluirTela, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.Delete);
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

    [EndpointSummary("Cadastrar tela")]
    public static async Task<Created<long>> CriarTela(ISender sender, CriarTelaCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar tela")]
    [EndpointDescription("Nome, localização e playlist exibida (vale na próxima sincronização da TV). Devolve a nova versão.")]
    public static async Task<Ok<uint>> AtualizarTela(
        ISender sender, long id, AtualizarTelaCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Gerar código de pareamento")]
    [EndpointDescription("Código de 8 caracteres, válido por 24 h e de uso único, para digitar na TV. Gerar de novo invalida o anterior.")]
    public static async Task<Ok<CodigoPareamentoDto>> GerarCodigoPareamento(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GerarCodigoPareamentoCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Desvincular TV")]
    [EndpointDescription("A TV pareada perde o acesso na próxima sincronização. Devolve a nova versão.")]
    public static async Task<Ok<uint>> DesvincularTela(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new DesvincularTelaCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Ativar tela")]
    public static async Task<Ok<uint>> AtivarTela(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new AtivarTelaCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Inativar tela")]
    [EndpointDescription("A TV deixa de receber grade (continua no que já baixou) e o código pendente é descartado.")]
    public static async Task<Ok<uint>> InativarTela(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new InativarTelaCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Excluir tela")]
    public static async Task<NoContent> ExcluirTela(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirTelaCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}
