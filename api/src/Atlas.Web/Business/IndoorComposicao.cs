using System.Security.Claims;
using System.Threading.RateLimiting;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Infrastructure.Business;
using Microsoft.AspNetCore.HttpOverrides;

namespace Atlas.Web.Business;

/// <summary>
/// Ponto único de composição da Mídia Indoor no <c>Program.cs</c> da
/// plataforma: <c>builder.AddIndoor()</c> e <c>app.UseIndoor()</c>. Manter o
/// contato com arquivos da plataforma nessas duas linhas reduz conflito no
/// merge de cada nova tag da Atlas Platform.
/// </summary>
public static class IndoorComposicao
{
    public static IHostApplicationBuilder AddIndoor(this IHostApplicationBuilder builder)
    {
        builder.AddBusinessServices();
        builder.Services.AddIndoorRateLimiting();
        builder.Services.AddTransient<IStartupFilter, CorsDoPainel>();

        // Antes do tratador da plataforma (AddWebServices vem depois no Program.cs).
        builder.Services.AddExceptionHandler<IndoorExceptionHandler>();

        return builder;
    }

    /// <summary>Middlewares do produto. Vai depois de <c>UseAuthorization</c>.</summary>
    public static WebApplication UseIndoor(this WebApplication app)
    {
        UsarIpRealAtrasDeProxy(app);
        app.UseRateLimiter();

        return app;
    }

    /// <summary>
    /// O limite de taxa do pareamento separa os clientes pelo IP. Atrás de
    /// balanceador ou CDN, com <c>Indoor:ProxiesConfiaveis</c> preenchido, o IP
    /// vem do <c>X-Forwarded-For</c>, aceito só desses proxies (sem a lista, o
    /// cabeçalho é ignorado: qualquer cliente poderia forjá-lo).
    /// </summary>
    private static void UsarIpRealAtrasDeProxy(WebApplication app)
    {
        var proxies = app.Configuration.GetSection("Indoor:ProxiesConfiaveis").Get<string[]>() ?? [];
        if (proxies.Length == 0)
        {
            return;
        }

        var opcoes = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
        opcoes.KnownProxies.Clear();
        opcoes.KnownNetworks.Clear();
        foreach (var ip in proxies)
        {
            opcoes.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
        }

        app.UseForwardedHeaders(opcoes);
    }

    private static void AddIndoorRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(IndoorRateLimits.Pareamento, httpContext =>
                Janela(httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido", limite: 10, TimeSpan.FromMinutes(5)));

            options.AddPolicy(IndoorRateLimits.Sincronizacao, httpContext =>
                Janela(httpContext.User.FindFirstValue(ClaimsDispositivo.TelaId)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "desconhecido", limite: 30, TimeSpan.FromMinutes(1)));
        });
    }

    private static RateLimitPartition<string> Janela(string chave, int limite, TimeSpan janela) =>
        RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = janela,
            QueueLimit = 0,
        });
}

/// <summary>Nomes das políticas de limite de taxa, usados em <c>RequireRateLimiting</c>.</summary>
public static class IndoorRateLimits
{
    /// <summary>
    /// Pareamento da TV: 10 tentativas por IP a cada 5 minutos. Com 32⁸
    /// códigos possíveis e validade de 24 h, adivinhar um código não é viável.
    /// </summary>
    public const string Pareamento = "indoor-pareamento";

    /// <summary>Sincronização: 30 por tela por minuto (o normal é 1 a cada 5 minutos).</summary>
    public const string Sincronizacao = "indoor-sincronizacao";
}
