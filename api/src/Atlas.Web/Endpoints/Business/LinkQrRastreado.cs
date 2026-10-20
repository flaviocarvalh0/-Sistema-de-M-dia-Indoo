using Atlas.Application.Business.Veiculacao;
using Atlas.Web.Business;

namespace Atlas.Web.Endpoints.Business;

/// <summary>
/// O endereço que o QR code da TV abre: <c>/q/{codigo}</c> conta a leitura e
/// redireciona para o WhatsApp, o Instagram ou o link do anunciante (ADR-023).
/// Endereço curto = QR menos denso, lido de mais longe.
/// </summary>
public class LinkQrRastreado : IEndpointGroup
{
    public static string? RoutePrefix => "/q";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .MapGet(Abrir, "{codigo}")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.LinkQr)
            .ExcludeFromDescription();
    }

    public static async Task<IResult> Abrir(HttpContext http, ISender sender, string codigo, CancellationToken cancellationToken)
    {
        var destino = await sender.Send(new RegistrarLeituraQrCommand(
            codigo,
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Headers.UserAgent.ToString()), cancellationToken);

        var cabecalhos = http.Response.Headers;
        // Cada leitura passa pela API (senão o celular reaproveita o redirecionamento e não conta).
        cabecalhos.CacheControl = "no-store";
        // O destino não fica sabendo de qual TV veio a leitura.
        cabecalhos["Referrer-Policy"] = "no-referrer";

        return destino is null
            ? Results.Content(PaginaLinkQr.NaoEncontrado, "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound)
            : Results.Redirect(destino.AbsoluteUri, permanent: false);
    }
}

internal static class PaginaLinkQr
{
    public const string NaoEncontrado = """
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Link indisponível</title></head>
        <body style="font-family:system-ui,sans-serif;max-width:28rem;margin:3rem auto;padding:0 1rem;text-align:center">
        <h1 style="font-size:1.4rem">Link indisponível</h1>
        <p>Este anúncio não está mais no ar.</p></body></html>
        """;
}
