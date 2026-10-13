using Atlas.Application.Common.Models.Pagination;
using Atlas.Application.Platform.SystemIcons.Commands.CreateSystemIcon;
using Atlas.Application.Platform.SystemIcons.Commands.DeleteSystemIcon;
using Atlas.Application.Platform.SystemIcons.Commands.UpdateSystemIcon;
using Atlas.Application.Platform.SystemIcons.Dtos;
using Atlas.Application.Platform.SystemIcons.Queries.GetSystemIconFile;
using Atlas.Application.Platform.SystemIcons.Queries.GetSystemIcons;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Platform;

public class SystemIcons : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Ícones do Sistema")
            .WithDescription("Gerenciamento do repositório central de ícones.");

        // Rota para listar o catálogo
        groupBuilder
            .MapGet("/", GetIcons)
            .RequireAuthorization(PermissionKeys.SystemIcons.View);

        // Rota para criar
        groupBuilder
            .MapPost("/", CreateIcon)
            .RequireAuthorization(PermissionKeys.SystemIcons.Create);

        // Rota para atualizar
        groupBuilder
            .MapPut("/{id:long}", UpdateIcon)
            .RequireAuthorization(PermissionKeys.SystemIcons.Update);

        // Rota para deletar
        groupBuilder
            .MapDelete("/{id:long}", DeleteIcon)
            .RequireAuthorization(PermissionKeys.SystemIcons.Delete);

        // Rota pública para renderizar a imagem no navegador (Com Cache)
        groupBuilder
            .MapGet("/{id:long}/render", RenderIcon)
            .AllowAnonymous();
    }

    [EndpointSummary("Consultar catálogo de ícones (Metadados)")]
    public static async Task<Ok<PaginatedList<SystemIconDto>>> GetIcons(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        var query = new GetSystemIconsQuery
        {
            PaginationParams = new PaginationParams
            {
                PageNumber = pageNumber ?? 1,
                PageSize = pageSize ?? 0 // Se vier nulo, fica 0, indicando que a extensão deve buscar o EntityMetadata!
            },
            SearchTerm = searchTerm
        };

        var result = await sender.Send(query, cancellationToken);

        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Fazer upload de um novo ícone")]
    public static async Task<Ok<CreatedSystemIconDto>> CreateIcon(
        ISender sender,
        CreateSystemIconCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Atualizar arquivo ou metadados de um ícone existente")]
    public static async Task<Results<Ok<uint>, BadRequest<string>>> UpdateIcon(
        long id,
        ISender sender,
        UpdateSystemIconCommand command,
        CancellationToken cancellationToken)
    {
        var commandWithId = command with { Id = id };
        var result = await sender.Send(commandWithId, cancellationToken);

        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(result.ErrorMessage ?? "Erro ao atualizar o ícone.");
        }

        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Remover um ícone do sistema")]
    public static async Task<Results<Ok, BadRequest<string>>> DeleteIcon(
        long id,
        [FromQuery] uint version, // Recebemos a versão da URL (ex: DELETE /api/system-icons/1?version=123)
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSystemIconCommand(id, version), cancellationToken);

        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(result.ErrorMessage ?? "Erro ao excluir o ícone.");
        }

        return TypedResults.Ok();
    }

    [EndpointSummary("Renderizar e armazenar o ícone em cache no navegador")]
    public static async Task<IResult> RenderIcon(
        long id,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSystemIconFileQuery(id), cancellationToken);

        if (!result.Succeeded || result.Value == null)
        {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers.CacheControl = "public,max-age=31536000";

        return TypedResults.File(result.Value.Data, result.Value.ContentType);
    }
}
