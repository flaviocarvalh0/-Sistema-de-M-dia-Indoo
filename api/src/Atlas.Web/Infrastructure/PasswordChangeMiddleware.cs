using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Atlas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Interceptador soberano de pipeline HTTP encarregado de isolar usuários em um limbo de segurança
/// caso possuam a flag de alteração de senha obrigatória ativada diretamente no banco de dados.
/// </summary>
/// <remarks>
/// Bloqueia requisições a quaisquer endpoints sob o prefixo <c>/api/</c>, exceto pelas rotas 
/// explícitas de autenticação necessárias para realizar a atualização cadastral ou o logout.
/// </remarks>
public class PasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public PasswordChangeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Executa o ciclo de interceptação avaliando o estado cadastral do usuário logado.
    /// </summary>
    /// <param name="httpContext">O contexto HTTP da requisição atual.</param>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var user = httpContext.User;

        // Executa a validação apenas se for uma rota de API e o usuário estiver devidamente autenticado
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            user?.Identity != null && user.Identity.IsAuthenticated)
        {
            // Como o Middleware é Singleton, resolvemos o UserManager do escopo atual da requisição
            var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var appUser = await userManager.FindByIdAsync(userId);

                // SEGUNDO ESCUDO SOBERANO: Valida a propriedade direto da tabela física do banco de dados
                if (appUser != null && appUser.MustChangePassword)
                {
                    // Rotas estritamente permitidas para que o usuário saia do limbo
                    bool isLoginRoute = path.EndsWith("/auth/login");
                    bool isChangePasswordRoute = path.EndsWith("/auth/change-password");
                    bool isLogoutRoute = path.EndsWith("/auth/logout");

                    if (!isLoginRoute && !isChangePasswordRoute && !isLogoutRoute)
                    {
                        httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        httpContext.Response.ContentType = "application/json";

                        // CONFIGURAÇÃO CRÍTICA: Permite acentuação em português (PT-BR) no JSON de resposta
                        var serializeOptions = new JsonSerializerOptions
                        {
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };

                        var errorResponse = JsonSerializer.Serialize(new
                        {
                            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                            title = "Alteração de Senha Obrigatória",
                            status = (int)HttpStatusCode.Forbidden,
                            detail = "Seu primeiro acesso ou redefinição exige a troca de senha. Altere sua senha para desbloquear o acesso aos demais recursos da plataforma."
                        }, serializeOptions);

                        await httpContext.Response.WriteAsync(errorResponse);
                        return; // Corta o fluxo e impede o request de chegar na controller
                    }
                }
            }
        }

        await _next(httpContext);
    }
}
