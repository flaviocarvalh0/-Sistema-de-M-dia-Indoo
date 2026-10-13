using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Models;
using Atlas.Application.Platform.Permissions.Commands.UpdateUserPermissions;
using Atlas.Application.Platform.Permissions.Dtos;
using Atlas.Application.Platform.Permissions.Queries.GetUserPermissions;
using Atlas.Application.Platform.Users.Commands.ActivateUser;
using Atlas.Application.Platform.Users.Commands.AssignUserRole;
using Atlas.Application.Platform.Users.Commands.CreateUser;
using Atlas.Application.Platform.Users.Commands.DeactivateUser;
using Atlas.Application.Platform.Users.Commands.RemoveUserRole;
using Atlas.Application.Platform.Users.Commands.ResetUserPassword;
using Atlas.Application.Platform.Users.Commands.UpdateUser;
using Atlas.Application.Platform.Users.Queries.GetUserById;
using Atlas.Application.Platform.Users.Queries.GetUsers;
using Atlas.Domain.Constants;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Platform;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Usuários")
            .WithDescription("API para provisionamento, auditoria e manutenção de contas e permissões no sistema.");

        groupBuilder
            .MapGet(GetUsers)
            .RequireAuthorization(PermissionKeys.Users.View);

        groupBuilder
            .MapGet(GetUserById, "{id}")
            .RequireAuthorization(PermissionKeys.Users.View);

        groupBuilder
            .MapPost(CreateUser)
            .RequireAuthorization(PermissionKeys.Users.Create);

        groupBuilder
            .MapPut(UpdateUser, "{id}")
            .RequireAuthorization(PermissionKeys.Users.Update);

        groupBuilder
            .MapPost(ActivateUser, "{id}/activate")
            .RequireAuthorization(PermissionKeys.Users.Activate);

        groupBuilder
            .MapPost(DeactivateUser, "{id}/deactivate")
            .RequireAuthorization(PermissionKeys.Users.Deactivate);

        groupBuilder
            .MapPost(ResetUserPassword, "{id}/reset-password")
            .RequireAuthorization(PermissionKeys.Users.ResetPassword);

        groupBuilder
            .MapPost(AssignUserRole, "{id}/roles")
            .RequireAuthorization(PermissionKeys.Users.AssignRole);

        groupBuilder
            .MapDelete(RemoveUserRole, "{id}/roles/{roleName}")
            .RequireAuthorization(PermissionKeys.Users.RemoveRole);

        groupBuilder
            .MapGet(GetUserPermissions, "{id}/permissions")
            .RequireAuthorization(PermissionKeys.Users.View);

        groupBuilder
            .MapPut(UpdateUserPermissions, "{id}/permissions")
            .RequireAuthorization(PermissionKeys.Roles.ManagePermissions);
    }

    [EndpointSummary("Listar usuários")]
    [EndpointDescription("Retorna a lista de usuários cadastrados na plataforma. Requer permissão Users.View.")]
    public static async Task<Ok<IReadOnlyCollection<UserDto>>> GetUsers(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var users = await sender.Send(new GetUsersQuery(), cancellationToken);

        return TypedResults.Ok(users);
    }

    [EndpointSummary("Obter usuário por ID")]
    [EndpointDescription("Retorna os dados de um usuário específico, incluindo dados cadastrais, status e grupos vinculados. Requer permissão Users.View.")]
    public static async Task<Results<Ok<UserDto>, NotFound>> GetUserById(
        ISender sender,
        long id,
        CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetUserByIdQuery
        {
            UserId = id
        }, cancellationToken);

        return user is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(user);
    }

    [EndpointSummary("Criar usuário")]
    [EndpointDescription("Cria um novo usuário interno da plataforma. O usuário pode ser criado com e-mail opcional, senha temporária e grupos iniciais. Requer permissão Users.Create.")]
    public static async Task<Created<long>> CreateUser(
        ISender sender,
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await sender.Send(command, cancellationToken);

        return TypedResults.Created($"/api/Users/{userId}", userId);
    }

    [EndpointSummary("Atualizar usuário")]
    [EndpointDescription("Atualiza os dados cadastrais de um usuário existente, como nome completo e e-mail. Requer permissão Users.Update.")]
    public static async Task<Results<NoContent, BadRequest<ValidationProblemDetails>>> UpdateUser(
        ISender sender,
        long id,
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with
        {
            UserId = id
        }, cancellationToken);

        ThrowIfFailure(nameof(UpdateUserCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Ativar usuário")]
    [EndpointDescription("Ativa um usuário previamente inativado, permitindo que ele volte a acessar a plataforma. Requer permissão Users.Activate.")]
    public static async Task<NoContent> ActivateUser(
        ISender sender,
        long id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateUserCommand
        {
            UserId = id
        }, cancellationToken);

        ThrowIfFailure(nameof(ActivateUserCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Inativar usuário")]
    [EndpointDescription("Inativa um usuário sem removê-lo fisicamente do banco de dados. Usuários inativos não devem conseguir realizar login. Requer permissão Users.Deactivate.")]
    public static async Task<NoContent> DeactivateUser(
        ISender sender,
        long id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateUserCommand
        {
            UserId = id
        }, cancellationToken);

        ThrowIfFailure(nameof(DeactivateUserCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Redefinir senha do usuário")]
    [EndpointDescription("Redefine a senha de um usuário com uma senha temporária. Opcionalmente força a troca da senha no próximo login. Requer permissão Users.ResetPassword.")]
    public static async Task<NoContent> ResetUserPassword(
        ISender sender,
        long id,
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with
        {
            UserId = id
        }, cancellationToken);

        ThrowIfFailure(nameof(ResetUserPasswordCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Adicionar usuário a um grupo")]
    [EndpointDescription("Vincula um usuário a um grupo/perfil existente, fazendo com que ele herde as permissões desse grupo. Requer permissão Users.AssignRole.")]
    public static async Task<NoContent> AssignUserRole(
        ISender sender,
        long id,
        AssignUserRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with
        {
            UserId = id
        }, cancellationToken);

        ThrowIfFailure(nameof(AssignUserRoleCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Remover usuário de um grupo")]
    [EndpointDescription("Remove o vínculo de um usuário com um grupo/perfil. O usuário deixa de herdar as permissões desse grupo. Requer permissão Users.RemoveRole.")]
    public static async Task<NoContent> RemoveUserRole(
        ISender sender,
        long id,
        string roleName,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveUserRoleCommand
        {
            UserId = id,
            RoleName = roleName
        }, cancellationToken);

        ThrowIfFailure(nameof(RemoveUserRoleCommand), result);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Obter permissões do usuário")]
    [EndpointDescription("Retorna a matriz de permissões herdadas e específicas de um usuário. Requer permissão Users.View.")]
    public static async Task<Results<Ok<UserPermissionsDto>, NotFound>> GetUserPermissions(
    long id,
    ISender sender,
    CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserPermissionsQuery { UserId = id }, cancellationToken);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    [EndpointSummary("Atualizar permissões do usuário")]
    [EndpointDescription("Atualiza manualmente a matriz de permissões de um usuário específico. Requer permissão Roles.ManagePermissions.")]
    public static async Task<NoContent> UpdateUserPermissions(
        ISender sender,
        long id,
        UpdateUserPermissionsCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.UserId)
        {
            throw new ValidationException(new[] {
                new ValidationFailure(nameof(command.UserId), "O ID na URL não coincide com o corpo da requisição.")
            });
        }

        var success = await sender.Send(command, cancellationToken);

        if (!success)
        {
            throw new ValidationException(new[] {
                new ValidationFailure("Update", "Falha ao persistir a atualização de permissões.")
            });
        }

        return TypedResults.NoContent();
    }


    private static void ThrowIfFailure(string propertyName, Result result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var failures = result.Errors.Select(error =>
            new ValidationFailure(propertyName, error));

        throw new ValidationException(failures);
    }
}