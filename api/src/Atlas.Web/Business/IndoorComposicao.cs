using Atlas.Infrastructure.Business;

namespace Atlas.Web.Business;

/// <summary>
/// Ponto único de composição da Mídia Indoor no <c>Program.cs</c> da
/// plataforma: <c>builder.AddIndoor()</c> e <c>app.UseIndoor()</c>. Manter o
/// contato com arquivos da plataforma nessas duas linhas reduz conflito no
/// merge de cada nova tag da Atlas Platform.
/// </summary>
/// <remarks>
/// O esquema de autenticação das TVs e os limites de taxa entram aqui na
/// etapa do dispositivo.
/// </remarks>
public static class IndoorComposicao
{
    public static IHostApplicationBuilder AddIndoor(this IHostApplicationBuilder builder)
    {
        builder.AddBusinessServices();

        // Antes do tratador da plataforma (AddWebServices vem depois no Program.cs).
        builder.Services.AddExceptionHandler<IndoorExceptionHandler>();

        return builder;
    }

    /// <summary>Middlewares do produto. Vai depois de <c>UseAuthorization</c>.</summary>
    public static WebApplication UseIndoor(this WebApplication app) => app;
}
