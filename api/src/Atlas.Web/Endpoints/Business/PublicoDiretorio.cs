using System.Security.Cryptography;
using Atlas.Application.Business.Diretorio;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Infrastructure.Business.Diretorio;
using Atlas.Web.Business;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

        groupBuilder
            .MapPost(RegistrarToque, "{codigo}/toque")
            .AllowAnonymous()
            .RequireRateLimiting(IndoorRateLimits.Diretorio)
            .ExcludeFromDescription();
    }

    /// <summary>
    /// Toque num contato ou em "Quer anunciar aqui?" (ADR-027). A página manda
    /// com <c>navigator.sendBeacon</c> e não espera a resposta: o link abre
    /// direto, sem passar pela API. Responde 204 mesmo quando não conta.
    /// </summary>
    public static async Task<IResult> RegistrarToque(
        HttpContext http,
        ISender sender,
        CacheDiretorio cache,
        ILogger<PaginaDiretorioTv> logger,
        string codigo,
        [FromQuery(Name = "e")] string? evento,
        [FromQuery(Name = "c")] long? clienteId,
        [FromQuery(Name = "t")] string? tipo,
        CancellationToken cancellationToken)
    {
        var diretorio = await cache.ObterAsync(sender, codigo, cancellationToken);
        if (diretorio is null)
        {
            return Results.NotFound();
        }

        EventoDiretorio? qual = evento switch
        {
            "contato" => EventoDiretorio.Contato,
            "anuncie" => EventoDiretorio.Anuncie,
            _ => null,
        };
        TipoQrCode? contato = Enum.TryParse<TipoQrCode>(tipo, ignoreCase: true, out var t) && Enum.IsDefined(t) ? t : null;

        if (qual is { } e && !EhRobo(http))
        {
            await RegistrarAsync(http, sender, logger, diretorio, e, clienteId, contato, cancellationToken);
        }

        http.Response.Headers.CacheControl = "no-store";
        return Results.NoContent();
    }

    /// <summary>
    /// Pré-visualização de link (WhatsApp, Telegram, buscadores) também abre a
    /// página: não é gente lendo o QR na TV.
    /// </summary>
    public static bool EhRobo(HttpContext http)
    {
        var navegador = http.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(navegador)
            || navegador.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || navegador.Contains("facebookexternalhit", StringComparison.OrdinalIgnoreCase)
            || navegador.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase)
            || navegador.Contains("preview", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Contar nunca derruba a página: falhou, fica no log.</summary>
    private static async Task RegistrarAsync(
        HttpContext http,
        ISender sender,
        ILogger logger,
        DiretorioPublicoDto diretorio,
        EventoDiretorio evento,
        long? clienteId,
        TipoQrCode? contato,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new RegistrarInteracaoDiretorioCommand(
                diretorio,
                evento,
                clienteId,
                contato,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString()), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível contar {Evento} no diretório da tela {TelaId}.", evento, diretorio.TelaId);
        }
    }

    public static async Task<IResult> GetPaginaDiretorio(
        HttpContext http, ISender sender, CacheDiretorio cache, ILogger<PaginaDiretorioTv> logger, string codigo, CancellationToken cancellationToken)
    {
        var diretorio = await cache.ObterAsync(sender, codigo, cancellationToken);
        if (diretorio is not null && !EhRobo(http))
        {
            await RegistrarAsync(http, sender, logger, diretorio, EventoDiretorio.Acesso, null, null, cancellationToken);
        }

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
