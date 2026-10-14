using System.Security.Cryptography;
using Atlas.Application.Business.Diretorio;
using Atlas.Infrastructure.Business.Diretorio;
using Atlas.Web.Business;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// Diretório público de uma TV em JSON (sem login). A página que o celular
/// abre é a de <see cref="PaginaDiretorioTv"/>; este é o mesmo conteúdo para
/// outros clientes.
/// </summary>
public class PublicoDiretorio : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/publico/diretorio";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Mídia Indoor — Público: Diretório")
            .WithDescription("Anunciantes da grade de uma TV que aceitaram aparecer, com os contatos.");

        groupBuilder
            .MapGet(GetDiretorio, "{codigo}")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.Diretorio);
    }

    [EndpointSummary("Diretório de uma TV")]
    [EndpointDescription("Código de 10 caracteres do QR code da TV. 404 se o código não existe, a tela está inativa ou o diretório está desligado. Guardado 60 s em cache.")]
    public static async Task<Results<Ok<DiretorioPublicoDto>, NotFound>> GetDiretorio(
        HttpContext http, ISender sender, CacheDiretorio cache, string codigo, CancellationToken cancellationToken)
    {
        var diretorio = await cache.ObterAsync(sender, codigo, cancellationToken);
        if (diretorio is null)
        {
            return TypedResults.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=60";
        return TypedResults.Ok(diretorio);
    }
}

/// <summary>
/// A página que o celular abre ao ler o QR code da TV: <c>/d/{codigo}</c>
/// (endereço curto = QR code menos denso, lido de mais longe).
/// </summary>
public class PaginaDiretorioTv : IEndpointGroup
{
    public static string? RoutePrefix => "/d";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .MapGet(GetPaginaDiretorio, "{codigo}")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.Diretorio)
            .ExcludeFromDescription();
    }

    public static async Task<IResult> GetPaginaDiretorio(
        HttpContext http, ISender sender, CacheDiretorio cache, string codigo, CancellationToken cancellationToken)
    {
        var diretorio = await cache.ObterAsync(sender, codigo, cancellationToken);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var cabecalhos = http.Response.Headers;
        cabecalhos.ContentSecurityPolicy = PaginaDiretorio.Politica(nonce);
        cabecalhos.XContentTypeOptions = "nosniff";
        // O site do anunciante não fica sabendo de qual TV veio a visita.
        cabecalhos["Referrer-Policy"] = "no-referrer";
        // O nonce muda a cada resposta: o navegador não guarda a página (o cache é o do servidor).
        cabecalhos.CacheControl = "no-store";

        return diretorio is null
            ? Results.Content(PaginaDiretorio.NaoEncontrada(nonce), "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound)
            : Results.Content(PaginaDiretorio.Renderizar(diretorio, nonce), "text/html; charset=utf-8");
    }
}
