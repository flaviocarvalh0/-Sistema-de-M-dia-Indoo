using Atlas.Application.Common.Interfaces;
using Atlas.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        // Provê o acesso ao usuário logado no escopo do contexto HTTP atual
        builder.Services.AddScoped<IUser, CurrentUser>();

        // Permite acessar o HttpContext de forma injetável em serviços de camadas inferiores
        builder.Services.AddHttpContextAccessor();

        // Registro e ativação da engine global de tratamento de erros no padrão RFC 9110 (Problem Details)
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        builder.Services.AddProblemDetails();

        // Desativa o filtro automático de validação de modelo para dar controle total ao FluentValidation + MediatR
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        // Habilita a exploração de metadados de endpoints necessários para ferramentas de documentação
        builder.Services.AddEndpointsApiExplorer();

        // Padroniza a serialização global do JSON para as Minimal APIs:
        // 1. Trafega Enums como texto amigável em vez de inteiros numéricos.
        // 2. Normaliza datas (DateTimeOffset) aplicando o fuso horário parametrizado no appsettings.
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.SerializerOptions.Converters.Add(new PlatformDateTimeOffsetConverter(builder.Configuration));
        });

        // Configuração do gerador OpenAPI (Swagger/Scalar) integrado com segurança via Bearer Token e tratamentos customizados
        builder.Services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<ApiExceptionOperationTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddDocumentTransformer<SwaggerTagsDocumentTransformer>();

            var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, "Atlas.Application.xml");

            if (File.Exists(applicationXmlPath))
            {
                options.AddSchemaTransformer(new XmlCommentsTransformer(applicationXmlPath));
            }
        });

        // Habilita a infraestrutura de Compartilhamento de Recursos de Origens Diferentes (CORS)
        builder.Services.AddCors();
    }
}
