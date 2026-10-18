using Atlas.Application.Business.Rede.Parceiros;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>Parceiros: os locais onde a operadora instala as TVs (não acessam o sistema).</summary>
public class RedeParceiros : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/parceiros";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Parceiros")
            .WithDescription("Estabelecimentos onde as TVs ficam. Quem anuncia é cliente.");

        groupBuilder.MapGet(GetParceiros).RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapGet(GetParceiroById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapPost(CriarParceiro).RequireAuthorization(BusinessPermissionKeys.Parceiros.Create);
        groupBuilder.MapPut(AtualizarParceiro, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);
        groupBuilder.MapPost(AtivarParceiro, "{id:long}/ativar").RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);
        groupBuilder.MapPost(InativarParceiro, "{id:long}/inativar").RequireAuthorization(BusinessPermissionKeys.Parceiros.Update);
        groupBuilder.MapDelete(ExcluirParceiro, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Parceiros.Delete);
    }

    [EndpointSummary("Listar parceiros")]
    [EndpointDescription("Lista paginada com busca por nome, responsável, cidade, endereço, telefone ou documento e filtro por ativo.")]
    public static async Task<Ok<PaginatedList<ParceiroDto>>> GetParceiros(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetParceirosQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            Ativo = ativo,
        }, cancellationToken));
    }

    [EndpointSummary("Obter parceiro")]
    public static async Task<Ok<ParceiroDto>> GetParceiroById(ISender sender, long id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new GetParceiroByIdQuery(id), cancellationToken));

    [EndpointSummary("Cadastrar parceiro")]
    [EndpointDescription("Só o cadastro: o parceiro não ganha acesso. Devolve o id.")]
    public static async Task<Created<long>> CriarParceiro(ISender sender, CriarParceiroCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar parceiro")]
    [EndpointDescription("Fuso e cidade valem para as TVs do local na próxima sincronização. Devolve a nova versão.")]
    public static async Task<Ok<uint>> AtualizarParceiro(ISender sender, long id, AtualizarParceiroCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Ativar parceiro")]
    public static async Task<Ok<uint>> AtivarParceiro(ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new DefinirAtivoParceiroCommand(id, true, version), cancellationToken));

    [EndpointSummary("Inativar parceiro")]
    [EndpointDescription("Sai da lista de escolha das telas; as TVs já instaladas seguem como estão.")]
    public static async Task<Ok<uint>> InativarParceiro(ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new DefinirAtivoParceiroCommand(id, false, version), cancellationToken));

    [EndpointSummary("Excluir parceiro")]
    [EndpointDescription("Só sem telas cadastradas no local.")]
    public static async Task<NoContent> ExcluirParceiro(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirParceiroCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}
