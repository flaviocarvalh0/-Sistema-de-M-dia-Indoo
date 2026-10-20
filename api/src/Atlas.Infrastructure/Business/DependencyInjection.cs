using System.Security.Claims;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Clima;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Application.Business.Conteudo.Cotacoes;
using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Business.Armazenamento;
using Atlas.Infrastructure.Business.Conteudo;
using Atlas.Infrastructure.Business.Dispositivo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Infrastructure.Business;

/// <summary>
/// Composição da área Business da Mídia Indoor. Fica fora do
/// <c>DependencyInjection</c> da plataforma para que as atualizações da Atlas
/// Platform (merge da tag) não conflitem com registros do produto — o único
/// ponto de contato é a chamada em <c>Program.cs</c>.
/// </summary>
public static class DependencyInjection
{
    public static void AddBusinessServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IClienteAtual, ClienteAtual>();
        builder.Services.AddSingleton<IConfiguracaoIndoor, ConfiguracaoIndoor>();
        builder.AddArmazenamentoMidias();
        builder.Services.AddAutenticacaoDispositivo(builder.Configuration);
        builder.Services.AddBusinessAuthorization();
        builder.Services.AddConteudoExterno();
    }

    /// <summary>
    /// Notícias (feeds RSS), clima (Open-Meteo) e cotações (PTAX do Banco Central): os coletores, o cliente HTTP
    /// e a coleta em segundo plano (<see cref="ColetorConteudoExterno"/>).
    /// </summary>
    private static void AddConteudoExterno(this IServiceCollection services)
    {
        services.AddSingleton<IClienteConteudoExterno>(_ => new ClienteConteudoExterno(ClienteConteudoExterno.CriarHttpClient()));
        services.AddScoped<ColetorNoticias>();
        services.AddScoped<ColetorClima>();
        services.AddScoped<ColetorCotacoes>();
        services.AddHostedService<ColetorConteudoExterno>();
        services.AddScoped<Atlas.Application.Business.Rede.Telas.Alertas.VerificadorTelasOffline>();
        services.AddScoped<TetoArmazenamento>();
        services.AddScoped<InventarioArmazenamentoServico>();
        services.AddHostedService<MonitorTelas>();
        services.AddHostedService<MonitorArmazenamento>();
    }

    /// <summary>
    /// Storage das mídias: Cloudflare R2 se <c>Indoor:Armazenamento:R2</c>
    /// estiver configurado (ADR-025), senão o Supabase. Sem nenhum, a API sobe
    /// e só o que mexe em arquivo responde com erro.
    /// </summary>
    /// <remarks>
    /// Com os dois configurados e <c>Indoor:Armazenamento:CopiarDoSupabase</c>
    /// ligado, a subida copia para o R2 o que ainda só está no Supabase.
    /// </remarks>
    private static void AddArmazenamentoMidias(this IHostApplicationBuilder builder)
    {
        var r2 = OpcoesR2.Ler(builder.Configuration);
        var supabase = OpcoesSupabase.Ler(builder.Configuration);

        if (r2 is null && supabase is null)
        {
            builder.Services.AddSingleton<IArmazenamentoMidias, ArmazenamentoNaoConfigurado>();
            return;
        }

        var copiar = r2 is not null && supabase is not null
            && builder.Configuration.GetValue<bool>("Indoor:Armazenamento:CopiarDoSupabase");

        if (supabase is not null && (r2 is null || copiar))
        {
            builder.Services.AddSingleton(supabase);
            builder.Services.AddSingleton(sp => new ArmazenamentoSupabase(
                CriarHttpClient(),
                supabase,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ArmazenamentoSupabase>>()));
        }

        if (r2 is null)
        {
            builder.Services.AddSingleton<IArmazenamentoMidias>(sp => sp.GetRequiredService<ArmazenamentoSupabase>());
            if (supabase!.CriarBucket)
            {
                builder.Services.AddHostedService<InicializadorBucket>();
            }

            return;
        }

        builder.Services.AddSingleton(r2);
        builder.Services.AddSingleton(sp => new ArmazenamentoR2(
            CriarHttpClient(),
            r2,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ArmazenamentoR2>>()));
        builder.Services.AddSingleton<IArmazenamentoMidias>(sp => sp.GetRequiredService<ArmazenamentoR2>());
        builder.Services.AddHostedService(sp => new InicializadorR2(
            sp.GetRequiredService<ArmazenamentoR2>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IConfiguracaoIndoor>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InicializadorR2>>(),
            copiar ? sp.GetRequiredService<ArmazenamentoSupabase>() : null));
    }

    /// <summary>Um HttpClient por storage, com conexões renovadas para acompanhar mudanças de DNS.</summary>
    private static HttpClient CriarHttpClient() =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

    /// <summary>
    /// Registra uma política por chave de <see cref="BusinessPermissionKeys"/>.
    /// </summary>
    /// <remarks>
    /// A plataforma só registra as próprias <c>PermissionKeys</c>; sem isto, um
    /// <c>[Authorize(Policy = "Telas.View")]</c> em command de negócio quebraria
    /// por política inexistente. A regra é a mesma da plataforma: superusuário
    /// passa; os demais precisam da claim de permissão exata.
    /// </remarks>
    public static IServiceCollection AddBusinessAuthorization(this IServiceCollection services)
    {
        var authorizationBuilder = services.AddAuthorizationBuilder();

        foreach (var permissao in BusinessPermissionKeys.All())
        {
            authorizationBuilder.AddPolicy(permissao, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => TemPermissaoAsync(context, permissao));
            });
        }

        return services;
    }

    private static async Task<bool> TemPermissaoAsync(AuthorizationHandlerContext context, string permissao)
    {
        // Fora de uma requisição HTTP (ex.: avaliação pelo IdentityService) não há
        // como resolver serviços: vale só a claim, como na plataforma.
        if (context.Resource is HttpContext httpContext
            && long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var usuarioId))
        {
            var identityService = httpContext.RequestServices.GetRequiredService<IIdentityService>();

            if (await identityService.IsSuperUserAsync(usuarioId))
            {
                return true;
            }
        }

        return context.User.HasClaim(c => c.Type == CustomClaimTypes.Permission && c.Value == permissao);
    }
}
