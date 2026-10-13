using Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.CriarMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EnvioMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EstadoMidia;
using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Business.Indoor.Midias.Queries;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Mídias do parceiro do usuário logado. O envio é em dois passos: <c>envio</c>
/// devolve a URL assinada, o navegador manda o arquivo direto ao storage e
/// <c>POST /</c> registra a mídia depois que a API confere o arquivo.
/// </summary>
public class ParceiroMidias : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/parceiro/midias";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Parceiro: Mídias")
            .WithDescription("Vídeos e imagens do parceiro do usuário logado.");

        groupBuilder.MapGet(GetMidias).RequireAuthorization(BusinessPermissionKeys.Midias.View);
        groupBuilder.MapGet(GetUsoArmazenamento, "armazenamento").RequireAuthorization(BusinessPermissionKeys.Midias.View);
        groupBuilder.MapGet(GetMidiaById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Midias.View);
        groupBuilder.MapPost(PrepararEnvio, "envio").RequireAuthorization(BusinessPermissionKeys.Midias.Create);
        groupBuilder.MapPost(CriarMidia).RequireAuthorization(BusinessPermissionKeys.Midias.Create);
        groupBuilder.MapPut(AtualizarMidia, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Midias.Update);
        groupBuilder.MapPost(AtivarMidia, "{id:long}/ativar").RequireAuthorization(BusinessPermissionKeys.Midias.Update);
        groupBuilder.MapPost(InativarMidia, "{id:long}/inativar").RequireAuthorization(BusinessPermissionKeys.Midias.Update);
        groupBuilder.MapDelete(ExcluirMidia, "{id:long}").RequireAuthorization(BusinessPermissionKeys.Midias.Delete);
    }

    [EndpointSummary("Listar mídias")]
    public static async Task<Ok<PaginatedList<MidiaDto>>> GetMidias(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] bool? ativo,
        [FromQuery] TipoMidia? tipo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiasQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            Ativo = ativo,
            Tipo = tipo,
        }, cancellationToken));
    }

    [EndpointSummary("Uso do armazenamento")]
    [EndpointDescription("Espaço usado, cota do parceiro e tamanho máximo por arquivo.")]
    public static async Task<Ok<UsoArmazenamentoDto>> GetUsoArmazenamento(ISender sender, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetUsoArmazenamentoQuery(), cancellationToken));
    }

    [EndpointSummary("Obter mídia")]
    [EndpointDescription("Inclui um endereço temporário (1 h) para pré-visualizar o arquivo.")]
    public static async Task<Ok<MidiaDto>> GetMidiaById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetMidiaByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Preparar envio de arquivo")]
    [EndpointDescription("Confere tipo, tamanho e cota e devolve a URL assinada: o navegador faz PUT do arquivo (corpo binário, Content-Type do arquivo) e depois chama POST / com a chave.")]
    public static async Task<Ok<EnvioMidiaDto>> PrepararEnvio(
        ISender sender, PrepararEnvioMidiaCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Cadastrar mídia")]
    [EndpointDescription("Depois do envio: a API lê o arquivo, confere tipo, tamanho e SHA-256 e grava a mídia. Arquivo recusado é apagado do storage.")]
    public static async Task<Created<long>> CriarMidia(
        ISender sender, CriarMidiaCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar mídia")]
    [EndpointDescription("Título, duração e período de exibição. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> AtualizarMidia(
        ISender sender, long id, AtualizarMidiaCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Ativar mídia")]
    public static async Task<Ok<uint>> AtivarMidia(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new AtivarMidiaCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Inativar mídia")]
    [EndpointDescription("Sai de todas as grades sem perder o lugar nas playlists.")]
    public static async Task<Ok<uint>> InativarMidia(
        ISender sender, long id, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new InativarMidiaCommand(id, version), cancellationToken));
    }

    [EndpointSummary("Excluir mídia")]
    [EndpointDescription("Apaga a mídia e o arquivo (libera a cota). Recusada se a mídia estiver em alguma playlist.")]
    public static async Task<NoContent> ExcluirMidia(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirMidiaCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}
