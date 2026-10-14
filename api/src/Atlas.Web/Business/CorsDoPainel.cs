namespace Atlas.Web.Business;

/// <summary>
/// Libera no CORS a origem do painel (<c>Indoor:UrlPainel</c>).
/// </summary>
/// <remarks>
/// O <c>Program.cs</c> da plataforma fixa as origens de desenvolvimento
/// (<c>http://localhost:4200</c> e afins), então o painel publicado em outro
/// endereço não conseguia chamar a API. Em vez de editar a lista da
/// plataforma, este filtro põe, no começo do pipeline, um <c>UseCors</c> só
/// para requisições vindas da origem do painel; as demais seguem para o CORS
/// da plataforma, que continua igual.
/// </remarks>
internal sealed class CorsDoPainel(IConfiguration configuration) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        var origem = Origem(configuration["Indoor:UrlPainel"]);
        if (origem is not null)
        {
            // Só para requisições dessa origem: o middleware de CORS encerra
            // todo preflight que trata, e as outras origens precisam chegar
            // ao CORS da plataforma.
            app.UseWhen(
                contexto => string.Equals(contexto.Request.Headers.Origin, origem, StringComparison.OrdinalIgnoreCase),
                ramo => ramo.UseCors(policy => policy
                    .WithOrigins(origem)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()));
        }

        next(app);
    };

    /// <summary><c>https://painel.exemplo.com.br/app/</c> → <c>https://painel.exemplo.com.br</c>.</summary>
    internal static string? Origem(string? urlPainel) =>
        Uri.TryCreate(urlPainel, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
}
