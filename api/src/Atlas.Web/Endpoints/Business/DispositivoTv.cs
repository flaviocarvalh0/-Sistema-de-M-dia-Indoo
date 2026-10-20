using Atlas.Application.Business.Dispositivo.Autenticacao;
using Atlas.Application.Business.Dispositivo.Exibicoes;
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
            .WithDescription("Pareamento, sincronização da grade e envio das exibições pelo app das TVs.");

        groupBuilder
            .MapPost(Autenticar, "autenticar")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.Pareamento);

        groupBuilder
            .MapGet(Sincronizar, "sincronizacao")
            .RequireAuthorization(AutenticacaoDispositivo.Politica)
            .RequireRateLimiting(IndoorRateLimits.Sincronizacao);

        groupBuilder
            .MapPost(RegistrarExibicoes, "exibicoes")
            .RequireAuthorization(AutenticacaoDispositivo.Politica)
            .RequireRateLimiting(IndoorRateLimits.Exibicoes);
    }

    [EndpointSummary("Enviar exibições")]
    [EndpointDescription("Lote de até 500 exibições de anúncio registradas pela TV (prova de veiculação). Idempotente pelo id de cada exibição: reenviar não duplica. 200 = a TV pode apagar o lote, inclusive as descartadas.")]
    public static async Task<Ok<RegistroExibicoesDto>> RegistrarExibicoes(
        ISender sender, RegistrarExibicoesCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Parear a TV")]
    [EndpointDescription("Troca o código de pareamento (8 caracteres, gerado no painel) pelo token da TV. Código inválido ou expirado dá 400 com a mesma mensagem, qualquer que seja o motivo.")]
    public static async Task<Ok<DispositivoAutenticadoDto>> Autenticar(
        ISender sender, AutenticarDispositivoCommand command, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(command, cancellationToken));
    }

    [EndpointSummary("Sincronizar a grade")]
    [EndpointDescription("Grade da tela com URLs de download, hash, dias e período de cada item, mais a composição da fila, as notícias e o clima. Com a versaoGrade atual, responde alterada=false sem itens; com a versaoNoticias atual, noticias vem nulo. 401: token revogado (parear de novo). 403: tela inativa (continuar no que já baixou).")]
    public static async Task<Ok<SincronizacaoDto>> Sincronizar(
        ISender sender,
        [FromQuery] string? versaoGrade,
        [FromQuery] string? versaoNoticias,
        [FromQuery] string? versaoApp,
        [FromQuery] int? versaoCodigoApp,
        [FromQuery] int? itensProntos,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await sender.Send(new GetSincronizacaoQuery
        {
            VersaoGrade = versaoGrade,
            VersaoNoticias = versaoNoticias,
            VersaoApp = versaoApp,
            VersaoCodigoApp = versaoCodigoApp,
            ItensProntos = itensProntos,
        }, cancellationToken));
    }
}
