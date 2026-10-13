using Atlas.Application.Platform.SystemModules.Commands.AssociateEntities;
using Atlas.Application.Platform.SystemModules.Commands.CreateSystemModule;
using Atlas.Application.Platform.SystemModules.Commands.UpdateSystemModule;
using Atlas.Application.Platform.SystemModules.Dtos;
using Atlas.Application.Platform.SystemModules.Queries.GetApplications;
using Atlas.Application.Platform.SystemModules.Queries.GetAvailableEntities;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform;

public class SystemModules : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Módulos do Sistema")
            .WithDescription("Endpoints para gestão dos módulos da plataforma, incluindo ordenação, rota base, ícone e associação com entidades configuráveis.");

        groupBuilder
            .MapGet("/", GetSystemModules)
            .RequireAuthorization(PermissionKeys.SystemModules.View);

        groupBuilder
            .MapGet("/available-entities", GetAvailableEntities)
            .RequireAuthorization(PermissionKeys.SystemModules.View);

        groupBuilder
            .MapPost("/", CreateSystemModule)
            .RequireAuthorization(PermissionKeys.SystemModules.Create);

        groupBuilder
            .MapPut("/{id}", UpdateSystemModule)
            .RequireAuthorization(PermissionKeys.SystemModules.Update);

        groupBuilder
            .MapPost("/associate-entities", AssociateEntities)
            .RequireAuthorization(PermissionKeys.SystemModules.Update);
    }

    [EndpointSummary("Listar módulos ativos")]
    [EndpointDescription("Retorna os módulos configurados na plataforma, incluindo rota base, ícone, ordenação e entidades vinculadas.")]
    public static async Task<Ok<List<SystemModuleDto>>> GetSystemModules(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSystemModulesQuery(), cancellationToken);
        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Listar entidades disponíveis")]
    [EndpointDescription("Retorna as entidades de metadados que ainda não possuem um módulo vinculado (utilizado para criação de novos módulos).")]
    public static async Task<Ok<List<AvailableEntityDto>>> GetAvailableEntities(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAvailableEntitiesQuery(), cancellationToken);
        return TypedResults.Ok(result.Value);
    }

    [EndpointSummary("Criar novo módulo")]
    [EndpointDescription("Instancia um novo módulo na plataforma definindo prefixo de rota, ordem de exibição e vincula uma lista inicial de entidades com auto-provisionamento de menu. Retorna o ID e a versão inicial (xmin).")]
    public static async Task<IResult> CreateSystemModule(CreateSystemModuleCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? TypedResults.Created($"/api/system-modules/{result.Value!.Id}", result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Atualizar módulo")]
    [EndpointDescription("Atualiza os metadados cadastrais do módulo (nome, descrição, rota, ordem e ícone) e retorna a nova versão (xmin).")]
    public static async Task<IResult> UpdateSystemModule(long id, UpdateSystemModuleCommand command, ISender sender, CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return TypedResults.BadRequest(new[] { "O ID da URL não corresponde ao ID do corpo da requisição." });

        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Associar tabelas ao módulo")]
    [EndpointDescription("Permite que o administrador acople metadados de tabelas descobertas automaticamente a um módulo de negócio específico com auto-provisionamento de menu.")]
    public static async Task<IResult> AssociateEntities(AssociateEntitiesCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Succeeded ? TypedResults.Ok() : TypedResults.BadRequest(result.Errors);
    }
}
