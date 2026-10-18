using Atlas.Application.Business.Rede.Clientes.Commands.AtualizarCliente;
using Atlas.Application.Business.Rede.Clientes.Commands.BloqueioCliente;
using Atlas.Application.Business.Rede.Clientes.Commands.CriarCliente;
using Atlas.Application.Business.Rede.Clientes.Dtos;
using Atlas.Application.Business.Rede.Clientes.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Clientes.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Clientes da rede, geridos pela operadora: cadastro, edição, bloqueio e consulta.</summary>
public class RedeClientes : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/clientes";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Clientes")
            .WithDescription("Clientes atendidos pela rede, como a operadora os vê.");

        groupBuilder
            .MapGet(GetClientes)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);

        groupBuilder
            .MapGet(GetClienteById, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Clientes.View);

        groupBuilder
            .MapPost(CriarCliente)
            .RequireAuthorization(BusinessPermissionKeys.Clientes.Create);

        groupBuilder
            .MapPut(AtualizarCliente, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Clientes.Update);

        groupBuilder
            .MapPost(BloquearCliente, "{id:long}/bloquear")
            .RequireAuthorization(BusinessPermissionKeys.Clientes.Update);

        groupBuilder
            .MapPost(DesbloquearCliente, "{id:long}/desbloquear")
            .RequireAuthorization(BusinessPermissionKeys.Clientes.Update);
    }

    [EndpointSummary("Listar clientes")]
    [EndpointDescription("Lista paginada com busca por nome, responsável, cidade, e-mail, telefone ou documento e filtro por situação.")]
    public static async Task<Ok<PaginatedList<ClienteDto>>> GetClientes(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] SituacaoCliente? situacao,
        CancellationToken cancellationToken)
    {
        var lista = await sender.Send(new GetClientesQuery
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

    [EndpointSummary("Obter cliente")]
    public static async Task<Ok<ClienteDto>> GetClienteById(
        ISender sender,
        long id,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetClienteByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Cadastrar cliente")]
    [EndpointDescription("Cria o cliente, o usuário do responsável com senha temporária e o vínculo, e envia o e-mail de boas-vindas. Devolve o id.")]
    public static async Task<Created<long>> CriarCliente(
        ISender sender,
        CriarClienteCommand command,
        CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar cliente")]
    [EndpointDescription("O e-mail de acesso não muda aqui. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> AtualizarCliente(
        ISender sender,
        long id,
        AtualizarClienteCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Bloquear cliente")]
    [EndpointDescription("Bloqueia com motivo obrigatório: a equipe perde o acesso e as TVs param de receber grade nova. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> BloquearCliente(
        ISender sender,
        long id,
        BloquearClienteCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Desbloquear cliente")]
    [EndpointDescription("Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> DesbloquearCliente(
        ISender sender,
        long id,
        DesbloquearClienteCommand command,
        CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }
}
