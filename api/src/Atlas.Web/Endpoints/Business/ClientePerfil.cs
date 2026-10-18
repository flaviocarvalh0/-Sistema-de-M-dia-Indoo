using Atlas.Application.Business.Clientes.Perfil;
using Atlas.Domain.Business.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Perfil público do cliente do usuário logado: o que aparece no diretório
/// das TVs e o aceite para aparecer (ADR-018).
/// </summary>
public class ClientePerfil : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/cliente/perfil";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Cliente: Perfil público")
            .WithDescription("Contatos e aceite do cliente para o diretório das TVs.");

        groupBuilder.MapGet(GetPerfilPublico).RequireAuthorization(BusinessPermissionKeys.Midias.View);
        groupBuilder.MapPut(AtualizarPerfilPublico, "").RequireAuthorization(BusinessPermissionKeys.Midias.Update);
    }

    [EndpointSummary("Obter perfil público")]
    public static async Task<Ok<PerfilPublicoDto>> GetPerfilPublico(ISender sender, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetPerfilPublicoQuery(), cancellationToken));
    }

    [EndpointSummary("Salvar perfil público")]
    [EndpointDescription("Categoria, descrição, WhatsApp, Instagram, site e o aceite para aparecer no diretório das TVs. Só o dono do cliente. Devolve a nova versão.")]
    public static async Task<Ok<uint>> AtualizarPerfilPublico(
        ISender sender, AtualizarPerfilPublicoCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }
}
