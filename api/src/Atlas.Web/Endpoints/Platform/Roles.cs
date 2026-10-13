using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Models;
using Atlas.Application.Platform.Permissions.Commands.UpdateRolePermissions;
using Atlas.Application.Platform.Permissions.Dtos;
using Atlas.Application.Platform.Permissions.Queries.GetRolePermissions;
using Atlas.Application.Platform.Roles.Commands.CreateRole;
using Atlas.Application.Platform.Roles.Commands.DeleteRole;
using Atlas.Application.Platform.Roles.Commands.UpdateRole;
using Atlas.Application.Platform.Roles.Dtos;
using Atlas.Application.Platform.Roles.Queries.GetRoles;
using Atlas.Domain.Constants;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;
public class Roles : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var rolesGroup = groupBuilder.WithTags("Grupos e Permissões");

        rolesGroup
            .MapGet("", GetAll)
            .RequireAuthorization(PermissionKeys.Roles.View);

        rolesGroup
            .MapPost("", Create)
            .RequireAuthorization(PermissionKeys.Roles.Create);

        rolesGroup
            .MapPut("{id:long}", Update)
            .RequireAuthorization(PermissionKeys.Roles.Update);

        rolesGroup
            .MapGet("{id:long}/permissions", GetPermissions)
            .RequireAuthorization(PermissionKeys.Roles.View);

        rolesGroup
            .MapPut("{id:long}/permissions", UpdatePermissions)
            .RequireAuthorization(PermissionKeys.Roles.Update);

        rolesGroup
            .MapDelete("{id:long}", Delete)
            .RequireAuthorization(PermissionKeys.Roles.Delete);
    }

    [EndpointSummary("Listar grupos")]
    [EndpointDescription("Retorna a listagem de todos os grupos de acesso cadastrados no ecossistema Atlas.")]
    public static async Task<Ok<IReadOnlyCollection<RoleDto>>> GetAll(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRolesQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Criar grupo")]
    public static async Task<Ok<long>> Create(
        ISender sender,
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        ThrowIfFailure(nameof(CreateRoleCommand), result);
        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Atualizar grupo")]
    public static async Task<Ok> Update(
        long id,
        ISender sender,
        UpdateRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            var failures = new[] { new ValidationFailure(nameof(UpdateRoleCommand.Id), "O ID fornecido na URL não coincide com o ID do corpo.") };
            throw new ValidationException(failures);
        }

        var result = await sender.Send(command, cancellationToken);
        ThrowIfFailure(nameof(UpdateRoleCommand), result);
        return TypedResults.Ok();
    }

    [EndpointSummary("Obter permissões do grupo")]
    [EndpointDescription("Busca a árvore completa de módulos e entidades, marcando quais permissões booleanas (View, Create, Update, Delete) o grupo possui ativas.")]
    public static async Task<Results<Ok<RolePermissionsDto>, NotFound>> GetPermissions(
        long id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRolePermissionsQuery { RoleId = id }, cancellationToken);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    [EndpointSummary("Atualizar matriz de permissões do grupo")]
    [EndpointDescription("Substitui em lote (Batch) a árvore de claims de um grupo, salvando o novo estado no banco de forma atômica.")]
    public static async Task<Ok> UpdatePermissions(
        long id,
        ISender sender,
        UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        // Garante a integridade blindando a rota contra IDs divergentes no payload
        if (id != command.RoleId)
        {
            var failures = new[] { new ValidationFailure(nameof(UpdateRolePermissionsCommand.RoleId), "O ID do grupo da URL não coincide com o corpo da requisição.") };
            throw new ValidationException(failures);
        }

        await sender.Send(command, cancellationToken);
        return TypedResults.Ok();
    }

    [EndpointSummary("Deletar grupo")]
    [EndpointDescription("Remove um grupo de acesso do ecossistema Atlas. O grupo de Administradores é protegido e não pode ser removido.")]
    public static async Task<Ok> Delete(
        long id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteRoleCommand(id), cancellationToken);

        ThrowIfFailure(nameof(DeleteRoleCommand), result);

        return TypedResults.Ok();
    }

    private static void ThrowIfFailure(string propertyName, Result result)
    {
        if (result.Succeeded) return;

        var failures = result.Errors.Select(error => new ValidationFailure(propertyName, error));
        throw new ValidationException(failures);
    }
}
