using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Adiciona o esquema de segurança JWT Bearer ao documento OpenAPI,
/// permitindo que a interface do Scalar envie o cabeçalho 'Authorization: Bearer &lt;token&gt;'.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();

        // Verifica se o nosso JWT Bearer ("Bearer") ou o do Identity está registrado
        if (authenticationSchemes.Any(authScheme => authScheme.Name == JwtBearerDefaults.AuthenticationScheme || authScheme.Name == "Identity.Bearer"))
        {
            // Usamos o nome padrão "Bearer" para casar perfeitamente com o AddJwtBearer
            const string schemeName = JwtBearerDefaults.AuthenticationScheme;

            var requirements = new Dictionary<string, OpenApiSecurityScheme>
            {
                [schemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer", // Obrigatório ser minúsculo para a especificação OpenAPI
                    In = ParameterLocation.Header,
                    BearerFormat = "JWT",
                    Description = "Insira o token JWT gerado pelo login. Exemplo: eyJhbGciOi..."
                }
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = requirements;

            // Aplica o requisito de segurança globalmente no documento OpenAPI.
            // Isso faz com que o Scalar exiba o cadeado/campo de token global na interface.
            document.SecurityRequirements = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeName } }] = Array.Empty<string>()
                }
            };
        }
    }
}
