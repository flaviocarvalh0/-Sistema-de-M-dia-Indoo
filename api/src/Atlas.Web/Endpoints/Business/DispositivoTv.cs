using Atlas.Application.Business.Dispositivo.Autenticacao;
using Atlas.Application.Business.Dispositivo.Sincronizacao;
using Atlas.Infrastructure.Business.Dispositivo;
using Atlas.Web.Business;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Rotas do app das TVs. Não usam login de usuário: o pareamento troca o
/// código do painel por um token próprio, enviado como
/// <c>Authorization: Bearer</c> na sincronização.
/// </summary>
public class DispositivoTv : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/dispositivo";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Dispositivo (TV)")
            .WithDescription("Pareamento e sincronização da grade pelo app das TVs.");

        groupBuilder
            .MapPost(Autenticar, "autenticar")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.Pareamento);

        groupBuilder
            .MapGet(Sincronizar, "sincronizacao")
            .RequireAuthorization(AutenticacaoDispositivo.Politica)
            .RequireRateLimiting(IndoorRateLimits.Sincronizacao);
    }

    [EndpointSummary("Parear a TV")]
    [EndpointDescription("Troca o código de pareamento (8 caracteres, gerado no painel) pelo token da TV. Código inválido ou expirado dá 400 com a mesma mensagem, qualquer que seja o motivo.")]
    public static async Task<Ok<DispositivoAutenticadoDto>> Autenticar(
        ISender sender, AutenticarDispositivoCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Sincronizar a grade")]
    [EndpointDescription("Grade da tela com URLs de download, hash, dias e período de cada item. Com a versaoGrade atual, responde alterada=false sem itens. 401: token revogado (parear de novo). 403: tela inativa ou parceiro bloqueado (continuar no que já baixou).")]
    public static async Task<Ok<SincronizacaoDto>> Sincronizar(
        ISender sender,
        [FromQuery] string? versaoGrade,
        [FromQuery] string? versaoApp,
        [FromQuery] int? itensProntos,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetSincronizacaoQuery
        {
            VersaoGrade = versaoGrade,
            VersaoApp = versaoApp,
            ItensProntos = itensProntos,
        }, cancellationToken));
    }
}
