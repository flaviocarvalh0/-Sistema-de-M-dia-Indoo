using Atlas.Application.Business.Rede.AppTv;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Versões do app das TVs (ADR-021): a operadora envia o APK e escolhe para
/// quem vai; as TVs atualizam sozinhas na sincronização.
/// </summary>
public class RedeAppTv : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/business/rede/app-tv";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Rede: App das TVs")
            .WithDescription("Versões do app das TVs e a distribuição de cada uma.");

        groupBuilder.MapGet(GetVersoesApp)
            .RequireAuthorization(BusinessPermissionKeys.Telas.View)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapPost(PrepararEnvioApk, "envio")
            .RequireAuthorization(BusinessPermissionKeys.Telas.Update)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapPost(CriarVersaoApp)
            .RequireAuthorization(BusinessPermissionKeys.Telas.Update)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapPost(DefinirDistribuicao, "{id:long}/distribuicao")
            .RequireAuthorization(BusinessPermissionKeys.Telas.Update)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
        groupBuilder.MapDelete(ExcluirVersaoApp, "{id:long}")
            .RequireAuthorization(BusinessPermissionKeys.Telas.Update)
            .RequireAuthorization(BusinessPermissionKeys.Parceiros.View);
    }

    [EndpointSummary("Listar versões do app")]
    [EndpointDescription("Versões enviadas (mais nova primeiro), a distribuição de cada uma e quantas TVs já rodam cada versão.")]
    public static async Task<Ok<VersoesAppDto>> GetVersoesApp(ISender sender, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetVersoesAppQuery(), cancellationToken));
    }

    [EndpointSummary("Preparar envio do APK")]
    [EndpointDescription("Devolve a chave e a URL assinada para o PUT do APK (Content-Type application/vnd.android.package-archive).")]
    public static async Task<Ok<EnvioApkDto>> PrepararEnvioApk(
        ISender sender, PrepararEnvioApkCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Registrar versão do app")]
    [EndpointDescription("Depois do envio: confere o APK (ZIP, até 50 MB, SHA-256) e registra a versão como suspensa.")]
    public static async Task<Created<long>> CriarVersaoApp(
        ISender sender, CriarVersaoAppCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return TypedResults.Created($"{RoutePrefix}/{id}", id);
    }

    [EndpointSummary("Definir para quem a versão vai")]
    [EndpointDescription("Suspensa (nenhuma TV), Teste (só as TVs de teste) ou Todas. Devolve a nova versão do registro.")]
    public static async Task<Ok<uint>> DefinirDistribuicao(
        ISender sender, long id, [FromQuery] DistribuicaoVersao distribuicao, [FromQuery] uint version, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new DefinirDistribuicaoVersaoCommand(id, distribuicao, version), cancellationToken));
    }

    [EndpointSummary("Excluir versão do app")]
    public static async Task<NoContent> ExcluirVersaoApp(ISender sender, long id, CancellationToken cancellationToken)
    {
        await sender.Send(new ExcluirVersaoAppCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
