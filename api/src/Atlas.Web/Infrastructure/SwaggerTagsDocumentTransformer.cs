using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Transformador soberano do documento OpenAPI responsável por forçar a ordenação da barra lateral 
/// do Scalar/Swagger, sobrepondo o comportamento padrão alfabético do framework.
/// </summary>
internal sealed class SwaggerTagsDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var ordemDesejada = new List<string>
        {
            "Introdução",
            "Autenticação",
            "Usuários",
            "Grupos e Permissões",
            "Módulos do Sistema",
            "Menus do Sistema",
            "Configuração de Entidades",
            "Ícones do Sistema",
            "Parâmetros do Sistema",
            "Servidor de E-mail",
            "Mensagens de E-mail",
            "Anexos de Entidades",
            "Histórico de Alterações",
            "Models"
        };

        var tagsOrdenadas = document.Tags
            .OrderBy(t =>
            {
                int index = ordemDesejada.IndexOf(t.Name);
                // Se a tag não estiver mapeada na lista acima, joga ela para o fim da fila alfabeticamente
                return index == -1 ? int.MaxValue : index;
            })
            .ThenBy(t => t.Name)
            .ToList();

        document.Tags = tagsOrdenadas;

        return Task.CompletedTask;
    }
}
