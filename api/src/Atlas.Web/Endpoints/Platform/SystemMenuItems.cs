using Atlas.Application.Platform.SystemMenus.Commands.CreateSystemMenuItem;
using Atlas.Application.Platform.SystemMenus.Commands.DeleteSystemMenuItem;
using Atlas.Application.Platform.SystemMenus.Commands.MoveSystemMenuItem;
using Atlas.Application.Platform.SystemMenus.Commands.ReorderSystemMenuItems;
using Atlas.Application.Platform.SystemMenus.Commands.UpdateSystemMenuItem;
using Atlas.Application.Platform.SystemMenus.Dtos;
using Atlas.Application.Platform.SystemMenus.Queries.GetModuleMenuTree;
using Atlas.Application.Platform.SystemMenus.Queries.GetSystemMenuTree;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;

public class SystemMenuItems : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Menus do Sistema")
            .WithDescription("Endpoints para gerenciamento das árvores de navegação dinâmicas da plataforma, organizadas por módulo.");

        groupBuilder
            .MapGet("/{moduleId:long}", GetMenuTree)
            .RequireAuthorization();

        groupBuilder
            .MapPost("/", CreateMenuItem)
            .RequireAuthorization(PermissionKeys.SystemMenus.Create);

        groupBuilder
            .MapPut("/{id:long}", UpdateMenuItem)
            .RequireAuthorization(PermissionKeys.SystemMenus.Update);

        groupBuilder
            .MapDelete("/{id:long}", DeleteMenuItem)
            .RequireAuthorization(PermissionKeys.SystemMenus.Delete);

        groupBuilder
            .MapPatch("/{id:long}/move", MoveMenuItem)
            .RequireAuthorization(PermissionKeys.SystemMenus.Update);

        groupBuilder
            .MapPut("/reorder", ReorderMenuItems)
            .RequireAuthorization(PermissionKeys.SystemMenus.Update);

        groupBuilder
            .MapGet("/modules/{moduleId:long}", GetModuleMenuTree)
            .RequireAuthorization(PermissionKeys.SystemMenus.View);
    }

    [EndpointSummary("Obter árvore de menu do módulo")]
    [EndpointDescription("Retorna a estrutura hierárquica de menus e submenus de um módulo, considerando as permissões do usuário autenticado.")]
    public static async Task<Ok<List<SystemMenuItemDto>>> GetMenuTree(
        long moduleId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSystemMenuTreeQuery(moduleId), cancellationToken);
        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Criar novo item de menu")]
    [EndpointDescription("Adiciona um nó raiz ou filho à árvore de navegação de um módulo, podendo atuar como agrupador ou apontar para uma entidade configurável.")]
    public static async Task<IResult> CreateMenuItem(
        CreateSystemMenuItemCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        // Retorna Created(URI, DTO contendo Id e Version)
        return result.Succeeded
            ? TypedResults.Created($"/api/system-menu-items/{result.Value!.Id}", result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Atualizar item de menu")]
    [EndpointDescription("Atualiza rótulo, tipo, ordenação, ícone, item pai e associação com entidade configurável.")]
    public static async Task<IResult> UpdateMenuItem(
    long id,
    UpdateSystemMenuItemCommand command,
    ISender sender,
    CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);

        // Retorna OK contendo a nova Version (uint) no body
        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Remover item de menu")]
    [EndpointDescription("Remove um item de menu e todos os seus subitens da árvore de navegação.")]
    public static async Task<IResult> DeleteMenuItem(
        long id,
        uint version,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSystemMenuItemCommand(id, version), cancellationToken);

        return result.Succeeded
            ? TypedResults.Ok()
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Mover item de menu")]
    [EndpointDescription("Move um item de menu para outro agrupador ou para a raiz do módulo.")]
    public static async Task<IResult> MoveMenuItem(
        long id,
        MoveSystemMenuItemCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { Id = id }, cancellationToken);

        return result.Succeeded
            ? TypedResults.Ok()
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Reordenar itens de menu")]
    [EndpointDescription("Atualiza a posição e a hierarquia de múltiplos itens de menu, útil para operações de drag-and-drop.")]
    public static async Task<IResult> ReorderMenuItems(
        ReorderSystemMenuItemsCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.Succeeded
            ? TypedResults.Ok()
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Obter árvore completa do módulo")]
    [EndpointDescription("Retorna a estrutura hierárquica completa de menus e submenus de um módulo, sem a aplicação de filtros de permissão. Utilizado para ferramentas de configuração e administração.")]
    public static async Task<Ok<List<SystemMenuItemDto>>> GetModuleMenuTree(
        long moduleId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetModuleMenuTreeQuery(moduleId), cancellationToken);
        return TypedResults.Ok(result.Value);
    }
}
