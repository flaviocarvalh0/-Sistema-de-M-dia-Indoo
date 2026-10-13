using Atlas.Application.Platform.EntityMetadata.Commands.UpdateEntityMetadata;
using Atlas.Application.Platform.EntityMetadata.Queries.GetEntityMetadata;
using Atlas.Domain.Constants;

namespace Atlas.Web.Endpoints.Platform;

public class EntityMetadata : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Configuração de Entidades")
            .WithDescription("Endpoints para gestão dinâmica das propriedades de exibição e comportamento das entidades da plataforma.");

        groupBuilder
            .MapGet("/", GetMetadata)
            .RequireAuthorization(PermissionKeys.EntityMetadata.View);

        groupBuilder
            .MapPut("/{id:long}", UpdateMetadata)
            .RequireAuthorization(PermissionKeys.EntityMetadata.Update);
    }

    [EndpointSummary("Listar metadados das entidades")]
    [EndpointDescription("Retorna a lista completa de entidades configuráveis e suas respectivas definições de exibição.")]
    public static async Task<IResult> GetMetadata(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetEntityMetadataQuery(), ct);
        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }

    [EndpointSummary("Atualizar configuração de entidade")]
    [EndpointDescription("Atualiza as definições de comportamento de uma entidade e retorna a nova versão (xmin) para controle de concorrência.")]
    public static async Task<IResult> UpdateMetadata(
        long id,
        UpdateEntityMetadataCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command with { Id = id }, ct);

        return result.Succeeded
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Errors);
    }
}
