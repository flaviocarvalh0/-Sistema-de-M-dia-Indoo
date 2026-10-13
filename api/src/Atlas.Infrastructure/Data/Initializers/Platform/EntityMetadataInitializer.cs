using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Common;
using Atlas.Domain.Platform.Attributes;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Data.Initializers.Platform;

/// <summary>
/// Inicializador responsável por garantir que as entidades do domínio marcadas com
/// <see cref="MetadataConfigurationAttribute"/> estejam devidamente registradas
/// na tabela de metadados da plataforma (<see cref="EntityMetadata"/>).
/// </summary>
public class EntityMetadataInitializer : IEntityMetadataInitializer
{
    private readonly IApplicationDbContext _context;

    public EntityMetadataInitializer(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureMetadataAsync(CancellationToken cancellationToken = default)
    {
        var domainAssembly = typeof(BaseAuditableEntity).Assembly;

        var entitiesWithMetadata = domainAssembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.GetCustomAttribute<MetadataConfigurationAttribute>() is not null)
            .ToList();

        foreach (var type in entitiesWithMetadata)
        {
            var attribute = type.GetCustomAttribute<MetadataConfigurationAttribute>()!;

            // 🌟 Obtém o nome físico convertido para snake_case no singular (ex: "system_module")
            var tableName = GetPhysicalTableName(type);
            var routeKey = attribute.RouteKey ?? ToRouteKey(tableName);

            var metadata = await _context.EntityMetadata
                .FirstOrDefaultAsync(x => x.TableName == tableName, cancellationToken);

            if (metadata is null)
            {
                _context.EntityMetadata.Add(new EntityMetadata
                {
                    TableName = tableName, // Salva em snake_case puro
                    DisplayLabel = attribute.DisplayLabel,
                    RouteKey = routeKey,
                    DefaultPageSize = attribute.DefaultPageSize,
                    EnableHistory = attribute.EnableHistory,
                    EnableExport = attribute.EnableExport,
                    CodePrefix = attribute.CodePrefix
                });

                continue;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Extrai o nome da classe ou do atributo Table e normaliza para snake_case no singular.
    /// </summary>
    private static string GetPhysicalTableName(Type type)
    {
        var tableAttribute = type.GetCustomAttribute<TableAttribute>();
        string rawName = type.Name;

        if (tableAttribute is not null && !string.IsNullOrWhiteSpace(tableAttribute.Name))
        {
            rawName = tableAttribute.Name;
        }

        // Converte de PascalCase para snake_case (Ex: "SystemModule" -> "system_module")
        return ToSnakeCase(rawName);
    }

    /// <summary>
    /// Transforma uma string PascalCase em snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Adiciona um underline antes de cada letra maiúscula e converte tudo para minúsculo
        var startUnderscore = Regex.Replace(input, "([a-z0-9])([A-Z])", "$1_$2");
        return startUnderscore.ToLowerInvariant().Trim('_');
    }

    /// <summary>
    /// Converte o nome da tabela em uma chave de rota kebab-case padronizada para as APIs.
    /// </summary>
    private static string ToRouteKey(string tableName)
    {
        // Como o tableName já vem em snake_case, trocamos apenas '_' por '-'
        return tableName.Replace('_', '-').ToLowerInvariant();
    }
}
