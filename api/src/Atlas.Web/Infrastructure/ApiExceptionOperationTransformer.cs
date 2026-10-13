using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Adiciona respostas de erro padrão a cada operação do OpenAPI. Um "400 Bad Request" é adicionado a todas
/// as operações porque cada requisição passa pelo <c>ValidationBehaviour</c> no pipeline do MediatR.
/// Respostas "401 Unauthorized" e "403 Forbidden" são adicionadas apenas a operações que possuem metadados <see cref="IAuthorizeData"/>.
/// </summary>
internal sealed class ApiExceptionOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Responses ??= [];

        // 1. Garante que o 400 Bad Request exiba o modelo com o nó 'errors' no Scalar
        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse
            {
                Description = "Ocorreram um ou mais erros de validação.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        // Cria a referência correta apontando para o Schema do ValidationProblemDetails
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ValidationProblemDetails) }
                        }
                    }
                }
            });
        }

        // Verifica se o endpoint exige autenticação através do atributo [Authorize] ou políticas do ASP.NET Core
        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .Any(m => m is IAuthorizeData);

        if (requiresAuth)
        {
            // 2. Garante o 401 Unauthorized com o Schema do ProblemDetails corporativo
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse
                {
                    Description = "Você precisa estar autenticado para acessar este recurso.",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ProblemDetails) }
                            }
                        }
                    }
                });
            }

            // 3. Garante o 403 Forbidden com o Schema do ProblemDetails corporativo
            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses.Add("403", new OpenApiResponse
                {
                    Description = "Você não tem permissão para acessar este recurso.",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = nameof(ProblemDetails) }
                            }
                        }
                    }
                });
            }
        }

        return Task.CompletedTask;
    }
}
