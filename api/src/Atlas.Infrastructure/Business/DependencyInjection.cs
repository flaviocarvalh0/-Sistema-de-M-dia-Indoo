using System.Security.Claims;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Business.Armazenamento;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        builder.Services.AddScoped<IParceiroAtual, ParceiroAtual>();
        builder.Services.AddSingleton<IConfiguracaoIndoor, ConfiguracaoIndoor>();
        builder.AddArmazenamentoMidias();
        builder.Services.AddBusinessAuthorization();
    }

    /// <summary>
    /// Storage das mídias (Supabase). Sem <c>Indoor:Armazenamento:Supabase</c>
    /// configurado, a API sobe e só o que mexe em arquivo responde com erro.
    /// </summary>
    private static void AddArmazenamentoMidias(this IHostApplicationBuilder builder)
    {
        var opcoes = OpcoesSupabase.Ler(builder.Configuration);

        if (opcoes is null)
        {
            builder.Services.AddSingleton<IArmazenamentoMidias, ArmazenamentoNaoConfigurado>();
            return;
        }

        builder.Services.AddSingleton(opcoes);

        // Um HttpClient só, com conexões renovadas para acompanhar mudanças de DNS.
        builder.Services.AddSingleton(sp => new ArmazenamentoSupabase(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                Timeout = TimeSpan.FromSeconds(60),
            },
            opcoes,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ArmazenamentoSupabase>>()));
        builder.Services.AddSingleton<IArmazenamentoMidias>(sp => sp.GetRequiredService<ArmazenamentoSupabase>());

        if (opcoes.CriarBucket)
        {
            builder.Services.AddHostedService<InicializadorBucket>();
        }
    }

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
