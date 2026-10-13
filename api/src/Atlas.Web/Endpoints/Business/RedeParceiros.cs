using Atlas.Application.Business.Rede.Parceiros.Commands.AtualizarParceiro;
using Atlas.Application.Business.Rede.Parceiros.Commands.BloqueioParceiro;
using Atlas.Application.Business.Rede.Parceiros.Commands.CriarParceiro;
using Atlas.Application.Business.Rede.Parceiros.Dtos;
using Atlas.Application.Business.Rede.Parceiros.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Parceiros.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Parceiros da rede, geridos pela operadora: cadastro, edição, bloqueio e consulta.</summary>
public class RedeParceiros : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/parceiros";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Parceiros")
            .WithDescription("Parceiros atendidos pela rede, como a operadora os vê.");

        groupBuilder
            .MapGet(GetParceiros)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);

        groupBuilder
            .MapGet(GetParceiroById, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);

        groupBuilder
            .MapPost(CriarParceiro)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.Create);

        groupBuilder
            .MapPut(AtualizarParceiro, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);

        groupBuilder
            .MapPost(BloquearParceiro, "{id:long}/bloquear")
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);

        groupBuilder
            .MapPost(DesbloquearParceiro, "{id:long}/desbloquear")
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);
    }

    [EndpointSummary("Listar parceiros")]
    [EndpointDescription("Lista paginada com busca por nome, responsável, cidade, e-mail, telefone ou documento e filtro por situação.")]
    public static async Task<Ok<PaginatedList<ParceiroDto>>> GetParceiros(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] SituacaoParceiro? situacao,
        CancellationToken cancellationToken)
    {
        var lista = await sender.Send(new GetParceirosQuery
        {
            PaginationParams = new PaginationParams
            {
                PageNumber = pageNumber ?? 1,
                PageSize = pageSize ?? 0
            },
            SearchTerm = searchTerm,
            Situacao = situacao
        }, cancellationToken);

        return TypedResults.Ok(lista);
    }

    [EndpointSummary("Obter parceiro")]
    public static async Task<Ok<ParceiroDto>> GetParceiroById(
        ISender sender,
        long id,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetParceiroByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Cadastrar parceiro")]
    [EndpointDescription("Cria o parceiro, o usuário do responsável com senha temporária e o vínculo, e envia o e-mail de boas-vindas. Devolve o id.")]
    public static async Task<Created<long>> CriarParceiro(
        ISender sender,
        CriarParceiroCommand command,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar parceiro")]
    [EndpointDescription("O e-mail de acesso não muda aqui. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> AtualizarParceiro(
        ISender sender,
        long id,
        AtualizarParceiroCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Bloquear parceiro")]
    [EndpointDescription("Bloqueia com motivo obrigatório: a equipe perde o acesso e as TVs param de receber grade nova. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> BloquearParceiro(
        ISender sender,
        long id,
        BloquearParceiroCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Desbloquear parceiro")]
    [EndpointDescription("Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> DesbloquearParceiro(
        ISender sender,
        long id,
        DesbloquearParceiroCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }
}
