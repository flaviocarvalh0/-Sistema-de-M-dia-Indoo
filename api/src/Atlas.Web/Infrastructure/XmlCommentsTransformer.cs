using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Transformador do OpenAPI responsável por extrair documentações XML geradas pelo compilador C#
/// e injetá-las dinamicamente nos schemas gerados no pipeline da API.
/// </summary>
/// <remarks>
/// Este componente traduz as tags nativas <c>&lt;summary&gt;</c> e <c>&lt;example&gt;</c> das camadas internas
/// (como Application e Domain) em metadados OpenAPI estruturados, permitindo que ferramentas visuais como
/// o Scalar exibam descrições detalhadas e payloads de exemplo interativos.
/// </remarks>
internal sealed class XmlCommentsTransformer : IOpenApiSchemaTransformer
{
    private readonly XPathNavigator? _xmlNavigator;

    /// <summary>
    /// Inicializa uma nova instância do transformador carregando e indexando o arquivo de documentação XML informado.
    /// </summary>
    /// <param name="xmlPath">Caminho físico absoluto para o arquivo .xml gerado no build do projeto.</param>
    public XmlCommentsTransformer(string xmlPath)
    {
        if (File.Exists(xmlPath))
        {
            var document = new XPathDocument(xmlPath);
            _xmlNavigator = document.CreateNavigator();
        }
    }

    /// <summary>
    /// Intercepta a geração do Schema do OpenAPI para mapear e injetar os comentários correspondentes às propriedades do tipo atual.
    /// </summary>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (_xmlNavigator is null || context.JsonTypeInfo.Type is null)
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;

        // =========================================================================
        // 🌟 BLOCO: Tratamento para Tipos Enum (como LoginMode)
        // =========================================================================
        if (type.IsEnum)
        {
            var memberName = $"T:{type.FullName}";
            var typeNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']");

            if (typeNode is not null)
            {
                var summaryNode = typeNode.SelectSingleNode("summary");
                if (summaryNode is not null)
                {
                    schema.Description = FormatXmlToMarkdown(summaryNode.InnerXml);
                }
            }
            return Task.CompletedTask;
        }

        // =========================================================================
        // Bloco Existente: Tratamento para Propriedades de Objetos (Commands/DTOs)
        // =========================================================================
        foreach (var property in schema.Properties)
        {
            // Busca os metadados ignorando Case para dar suporte a propriedades vindas em camelCase do front/JSON
            var propInfo = type.GetProperty(
                property.Key,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (propInfo is null) continue;

            // Constrói a assinatura do membro no formato padrão do arquivo XML (.NET compiler standard)
            var memberName = $"P:{propInfo.DeclaringType?.FullName}.{propInfo.Name}";
            var propertyNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']");

            if (propertyNode is null) continue;

            // 1. INJETA O SUMMARY (Sempre que existir!)
            var summaryNode = propertyNode.SelectSingleNode("summary");
            if (summaryNode is not null)
            {
                property.Value.Description = FormatXmlToMarkdown(summaryNode.InnerXml);
            }

            // 2. INJETA O EXAMPLE (Se existir de forma opcional)
            var exampleNode = propertyNode.SelectSingleNode("example");
            if (exampleNode is not null)
            {
                var exampleValue = exampleNode.Value.Trim();

                if (exampleValue.StartsWith("[") && exampleValue.EndsWith("]"))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<string>>(exampleValue);
                        if (list is not null)
                        {
                            var openApiArray = new OpenApiArray();
                            openApiArray.AddRange(list.Select(item => new OpenApiString(item)));
                            property.Value.Example = openApiArray;
                        }
                    }
                    catch
                    {
                        property.Value.Example = new OpenApiString(exampleValue);
                    }
                }
                else
                {
                    property.Value.Example = new OpenApiString(exampleValue);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Converte tags estruturadas do XML de documentação do C# para a sintaxe limpa do Markdown compatível com o Scalar.
    /// </summary>
    private static string FormatXmlToMarkdown(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return string.Empty;

        // 1. Normaliza as tags <para> internas transformando em quebras de linha limpas
        var result = xmlContent.Replace("<para>", "").Replace("</para>", "\n");

        // 2. Substitui marcadores de bolinha comuns (•) pelo padrão markdown de listas (* )
        result = result.Replace("•", "* ");

        // 3. Remove múltiplos espaços gerados pelas indentações do editor C# mantendo a estrutura
        result = Regex.Replace(result, @"^[ \t]+", "", RegexOptions.Multiline);

        return result.Trim();
    }
}
