using Atlas.Application.Business.Diretorio;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Rede.Telas.Commands.EstadoTela;
using Atlas.Application.Business.Rede.Telas.Commands.Pareamento;
using Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;
using Atlas.Application.Business.Rede.Telas.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Telas (TVs) da rede, geridas pela operadora, dona do hardware: cadastro,
/// parceiro, pareamento e situação. O parceiro só escolhe a playlist
/// (<see cref="ClienteTelas"/>).
/// </summary>
public class RedeTelas : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/telas";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Telas")
            .WithDescription("TVs de todos os parceiros, como a operadora as vê.");

        // Telas.View sozinho o parceiro também tem: Parceiros.View restringe à operadora.
        groupBuilder.MapGet(GetTelasDaRede)
            .RequireAuthorization(BusinessPermissionKeys.Telas.View)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapGet(GetTelaDaRedeById, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Telas.View)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapPost(CriarTela).RequireAuthorization(BusinessPermissionKeys.Telas.Create);
        groupBuilder.MapPut(AtualizarTela, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(GerarCodigoPareamento, "{id:long}/codigo-pareamento").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(TrocarCodigoDiretorio, "{id:long}/codigo-diretorio").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapGet(GetUsoDiretorio, "{id:long}/diretorio/uso").RequireAuthorization(BusinessPermissionKeys.Telas.View);
        groupBuilder.MapPost(DesvincularTela, "{id:long}/desvincular").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(AtivarTela, "{id:long}/ativar").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapPost(InativarTela, "{id:long}/inativar").RequireAuthorization(BusinessPermissionKeys.Telas.Update);
        groupBuilder.MapDelete(ExcluirTela, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Telas.Delete);
    }

    [EndpointSummary("Listar telas da rede")]
    [EndpointDescription("Todas as telas, com o parceiro, a situação (aguardando pareamento, online, offline) e o código pendente. Busca por nome, parceiro, local ou identificação do aparelho.")]
    public static async Task<Ok<PaginatedList<TelaDto>>> GetTelasDaRede(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] long? parceiroId,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelasRedeQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            ParceiroId = parceiroId,
            Ativo = ativo,
        }, cancellationToken));
    }

    [EndpointSummary("Obter tela")]
    public static async Task<Ok<TelaDto>> GetTelaDaRedeById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetTelaRedeByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Cadastrar tela")]
    public static async Task<Created<long>> CriarTela(ISender sender, CriarTelaCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar tela")]
    [EndpointDescription("Parceiro, nome, local e identificação do aparelho. Trocar o parceiro tira a playlist, desvincula a TV e descarta o código pendente. Devolve a nova versão.")]
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

    [EndpointSummary("Trocar o endereço do diretório")]
    [EndpointDescription("Gera outro código para a página pública da TV; o endereço anterior para de funcionar e a TV mostra o QR novo na próxima sincronização. Devolve o código, o endereço e a nova versão.")]
    public static async Task<Ok<CodigoDiretorioDto>> TrocarCodigoDiretorio(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new TrocarCodigoDiretorioCommand(id, version), cancellationToken));
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

    [EndpointSummary("Uso do diretório da TV")]
    [EndpointDescription("Acessos à página do diretório (QR lido), pessoas por dia e toques nos contatos e em \"Quer anunciar aqui?\" nos últimos dias (padrão 30, máximo 93).")]
    public static async Task<Ok<UsoDiretorioDto>> GetUsoDiretorio(ISender sender, long id, [FromQuery] int? dias, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetUsoDiretorioTelaQuery(id, dias ?? 30), cancellationToken));
    }
}
