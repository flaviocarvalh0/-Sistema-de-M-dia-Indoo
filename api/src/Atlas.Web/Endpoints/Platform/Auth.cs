using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Models;
using Atlas.Application.Platform.Auth.Commands.ChangePassword;
using Atlas.Application.Platform.Auth.Commands.Login;
using Atlas.Application.Platform.Auth.Commands.Logout;
using Atlas.Application.Platform.Auth.Commands.RefreshToken;
using Atlas.Application.Platform.Auth.Queries.GetCurrentUser;
using Atlas.Application.Platform.Auth.Queries.GetMyPermissions;
using Atlas.Application.Platform.Permissions.Dtos;
using Atlas.Application.Platform.SystemModules.Queries.GetUserModules;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;

public class Auth : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var authGroup = groupBuilder.WithTags("Autenticação");

        groupBuilder.MapPost(Login, "login");

        groupBuilder
            .MapPost(Logout, "logout")
            .RequireAuthorization();

        groupBuilder
            .MapPost(ChangePassword, "change-password")
            .RequireAuthorization();

        groupBuilder
            .MapGet(GetCurrentUser, "me")
            .RequireAuthorization();

        groupBuilder.MapPost(RefreshToken, "refresh");

        groupBuilder
            .MapGet(GetMyModules, "me/modules")
            .RequireAuthorization();

        groupBuilder
            .MapGet(GetMyPermissions, "me/permissions")
            .RequireAuthorization();
    }

    [EndpointSummary("Log in")]
    [EndpointDescription("Autentica o usuário usando login interno e senha. Use Mode=Cookie para aplicação Angular SPA com cookie HttpOnly ou Mode=Bearer para retornar um access token para aplicativo mobile.")]
    public static async Task<Ok<LoginResponse>> Login(
        ISender sender,
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        ThrowIfFailure(nameof(LoginCommand), result);

        return TypedResults.Ok(result.Value!);
    }

    [EndpointSummary("Refresh token")]
    [EndpointDescription("Gera um novo access token a partir de um refresh token válido. O refresh token é rotacionado a cada uso.")]
    public static async Task<Ok<RefreshTokenResponse>> RefreshToken(
        ISender sender,
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        ThrowIfFailure(nameof(RefreshTokenCommand), result);

        return TypedResults.Ok(result.Value!);
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Desconecta o usuário autenticado, removendo o cookie de autenticação quando estiver usando autenticação baseada em cookie.")]
    public static async Task<Ok> Logout(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LogoutCommand(), cancellationToken);

        ThrowIfFailure(nameof(LogoutCommand), result);

        return TypedResults.Ok();
    }

    [EndpointSummary("Alterar senha")]
    [EndpointDescription("Altera a senha do usuário autenticado. Também remove a obrigatoriedade de troca de senha quando a nova senha é definida com sucesso.")]
    public static async Task<Ok> ChangePassword(
        ISender sender,
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        ThrowIfFailure(nameof(ChangePasswordCommand), result);

        return TypedResults.Ok();
    }

    [EndpointSummary("Usuário atual")]
    [EndpointDescription("Retorna os dados do usuário autenticado, incluindo status, grupos vinculados e informações básicas de identidade.")]
    public static async Task<Ok<UserDto?>> GetCurrentUser(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetCurrentUserQuery(), cancellationToken);

        return TypedResults.Ok<UserDto?>(user);
    }

    [EndpointSummary("Módulos do usuário")]
    [EndpointDescription("Retorna a lista de módulos permitidos para o usuário logado.")]
    public static async Task<Ok<List<UserModuleDto>>> GetMyModules(
    ISender sender,
    CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserModulesQuery(), cancellationToken);

        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("Modules", e)));
        }

        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Permissões do usuário atual")]
    [EndpointDescription("Retorna a matriz completa de módulos e permissões associadas ao usuário atualmente autenticado.")]
    public static async Task<Results<Ok<UserPermissionsDto>, NotFound>> GetMyPermissions(
    ISender sender,
    CancellationToken cancellationToken)
    {
        var permissions = await sender.Send(new GetMyPermissionsQuery(), cancellationToken);

        return permissions is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(permissions);
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
