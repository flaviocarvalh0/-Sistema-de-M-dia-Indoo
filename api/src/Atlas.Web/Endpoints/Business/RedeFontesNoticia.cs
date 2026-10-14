using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Application.Business.Rede.FontesNoticia;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Feeds de notícias da rede (RSS ou Atom), cadastrados pela operadora. As
/// playlists decidem se intercalam notícias entre os anúncios.
/// </summary>
public class RedeFontesNoticia : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/fontes-noticia";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: Fontes de notícias")
            .WithDescription("Feeds RSS/Atom lidos a cada hora; as notícias vão para as TVs com a imagem.");

        groupBuilder.MapGet(GetFontesNoticia).RequireAuthorization(BusinessPermissionKeys.FonteNoticias.View);
        groupBuilder.MapGet(GetFonteNoticiaById, "{id:long}").RequireAuthorization(BusinessPermissionKeys.FonteNoticias.View);
        groupBuilder.MapGet(GetNoticiasDaFonte, "{id:long}/noticias").RequireAuthorization(BusinessPermissionKeys.FonteNoticias.View);
        groupBuilder.MapPost(CriarFonteNoticia).RequireAuthorization(BusinessPermissionKeys.FonteNoticias.Create);
        groupBuilder.MapPut(AtualizarFonteNoticia, "{id:long}").RequireAuthorization(BusinessPermissionKeys.FonteNoticias.Update);
        groupBuilder.MapPost(ColetarFonteNoticia, "{id:long}/coletar").RequireAuthorization(BusinessPermissionKeys.FonteNoticias.Update);
        groupBuilder.MapDelete(ExcluirFonteNoticia, "{id:long}").RequireAuthorization(BusinessPermissionKeys.FonteNoticias.Delete);
    }

    [EndpointSummary("Listar fontes de notícias")]
    public static async Task<Ok<PaginatedList<FonteNoticiaDto>>> GetFontesNoticia(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetFontesNoticiaQuery
        {
            PaginationParams = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 0 },
            SearchTerm = searchTerm,
            Ativo = ativo,
        }, cancellationToken));
    }

    [EndpointSummary("Obter fonte de notícias")]
    public static async Task<Ok<FonteNoticiaDto>> GetFonteNoticiaById(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetFonteNoticiaByIdQuery(id), cancellationToken));
    }

    [EndpointSummary("Notícias guardadas da fonte")]
    [EndpointDescription("As mais recentes primeiro, com o endereço temporário (1 h) da imagem.")]
    public static async Task<Ok<IReadOnlyList<NoticiaDto>>> GetNoticiasDaFonte(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetNoticiasDaFonteQuery(id), cancellationToken));
    }

    [EndpointSummary("Cadastrar fonte de notícias")]
    public static async Task<Created<long>> CriarFonteNoticia(
        ISender sender, CriarFonteNoticiaCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Atualizar fonte de notícias")]
    [EndpointDescription("Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> AtualizarFonteNoticia(
        ISender sender, long id, AtualizarFonteNoticiaCommand command, CancellationToken cancellationToken)
    {
        RotaIndoor.GarantirMesmoId(id, command.Id);

        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Coletar agora")]
    [EndpointDescription("Lê o feed na hora. Devolve quantas notícias entraram e saíram, ou o erro da leitura.")]
    public static async Task<Ok<ResultadoColetaNoticias>> ColetarFonteNoticia(ISender sender, long id, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new ColetarFonteNoticiaCommand(id), cancellationToken));
    }

    [EndpointSummary("Excluir fonte de notícias")]
    [EndpointDescription("Apaga também as notícias e as imagens guardadas.")]
    public static async Task<NoContent> ExcluirFonteNoticia(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirFonteNoticiaCommand(id), cancellationToken);

        return TypedResults.NoContent();
    }
}
