using Atlas.Application.Common.Models.Pagination;
using Atlas.Application.Platform.SystemParameters.Commands.CreateSystemParameter;
using Atlas.Application.Platform.SystemParameters.Commands.DeleteSystemParameter;
using Atlas.Application.Platform.SystemParameters.Commands.UpdateSystemParameter;
using Atlas.Application.Platform.SystemParameters.Queries.GetExistingGroups;
using Atlas.Application.Platform.SystemParameters.Queries.GetSystemParameters;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Platform;

public class SystemParameters : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Parâmetros do Sistema")
            .WithDescription("Gerenciamento de chaves e variáveis globais da plataforma.");

        // CRUD Operacional
        groupBuilder.MapGet("/", GetParameters)
            .RequireAuthorization(PermissionKeys.Parameters.View);

        groupBuilder.MapPost("/", CreateParameter)
            .RequireAuthorization(PermissionKeys.Parameters.Create);

        groupBuilder.MapPut("/{id:long}", UpdateParameter)
            .RequireAuthorization(PermissionKeys.Parameters.Update);

        groupBuilder.MapDelete("/{key}", DeleteParameter)
            .RequireAuthorization(PermissionKeys.Parameters.Delete);

        // Auxiliares p/ Alimentar Combos
        groupBuilder.MapGet("/data-types", GetDatatypes)
            .RequireAuthorization(PermissionKeys.Parameters.View);

        groupBuilder.MapGet("/groups", GetExistingGroups)
            .RequireAuthorization(PermissionKeys.Parameters.View);
    }

    [EndpointSummary("Listar parâmetros globais com auditoria e paginação")]
    public static async Task<Ok<PaginatedList<SystemParameterDto>>> GetParameters(
        ISender sender,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        var query = new GetSystemParametersQuery
        {
            PaginationParams = new PaginationParams
            {
                PageNumber = pageNumber ?? 1,
                PageSize = pageSize ?? 0
            },
            SearchTerm = searchTerm
        };

        var result = await sender.Send(query, cancellationToken);

        return TypedResults.Ok(result.Value!);
    }

    [EndpointSummary("Cadastrar novo parâmetro")]
    public static async Task<IResult> CreateParameter([FromBody] CreateSystemParameterCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        // Retorna HTTP 201 com URL do parâmetro e o payload { key, version }
        return result.Succeeded
            ? TypedResults.Created($"/api/system-parameters/{result.Value!.Key}", result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Atualizar valor de um parâmetro")]
    public static async Task<IResult> UpdateParameter(
    long id,
    [FromBody] UpdateSystemParameterCommand command,
    ISender sender,
    CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return TypedResults.BadRequest(new[] { "O ID da URL não corresponde ao ID do corpo da requisição." });

        var result = await sender.Send(command, cancellationToken);

        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Remover um parâmetro do sistema")]
    public static async Task<IResult> DeleteParameter(
        [FromRoute] string key,
        [FromQuery] uint version,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSystemParameterCommand(key, version), cancellationToken);

        return result.Succeeded
            ? TypedResults.NoContent()
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Listar tipos de dados aceitos")]
    public static IResult GetDatatypes()
    {
        return TypedResults.Ok(new[] { "String", "Number", "Boolean", "Json" });
    }

    [EndpointSummary("Listar grupos atualmente cadastrados (Autocomplete)")]
    public static async Task<IResult> GetExistingGroups(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExistingGroupsQuery(), cancellationToken);
        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }
}
