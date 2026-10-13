using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Atlas.Application.Common.Interfaces;

namespace Atlas.Infrastructure.Observability;

/// <summary>
/// Serviço responsável por gerar uma representação segura de objetos antes de enviá-los aos logs.
/// </summary>
/// <remarks>
/// O sanitizador percorre objetos complexos por Reflection e aplica as regras definidas pelos atributos
/// <see cref="LogSensitiveAttribute"/> e <see cref="LogIgnoreAttribute"/>.
/// 
/// Propriedades marcadas com <see cref="LogSensitiveAttribute"/> são mascaradas.
/// Propriedades marcadas com <see cref="LogIgnoreAttribute"/> são omitidas do resultado.
/// 
/// Este serviço não altera o objeto original; ele cria uma estrutura sanitizada, geralmente baseada em
/// dicionários e listas, própria para ser desestruturada pelo mecanismo de logging.
/// </remarks>
public sealed class LogSanitizer : ILogSanitizer
{
    private const string Mask = "***MASKED***";

    private const string BinaryContentOmitted =
        "[Binary content omitted]";

    // Cache de propriedades para evitar perda de performance com Reflection a cada log.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>
    /// Sanitiza o valor informado, mascarando ou omitindo propriedades conforme os atributos
    /// de observabilidade aplicados no modelo.
    /// </summary>
    /// <param name="value">
    /// Objeto, coleção, dicionário ou valor simples que será preparado para escrita segura nos logs.
    /// </param>
    /// <returns>
    /// Uma representação segura do valor informado. Objetos complexos são convertidos em dicionários,
    /// coleções são convertidas em listas sanitizadas e tipos simples são retornados sem alteração.
    /// </returns>
    public object? Sanitize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        // Conteúdo binário nunca deve ser expandido em logs.
        if (value is byte[])
        {
            return BinaryContentOmitted;
        }

        var type = value.GetType();

        if (IsSimpleType(type))
        {
            return value;
        }

        // Tratamento para dicionários.
        // Deve vir antes do IEnumerable, pois Dictionary também implementa IEnumerable.
        if (value is IDictionary dictionary)
        {
            var sanitizedDict = new Dictionary<string, object?>();

            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                sanitizedDict[key] = Sanitize(entry.Value);
            }

            return sanitizedDict;
        }

        // Tratamento para coleções/listas.
        if (value is IEnumerable enumerable && value is not string)
        {
            var sanitizedList = new List<object?>();

            foreach (var item in enumerable)
            {
                sanitizedList.Add(Sanitize(item));
            }

            return sanitizedList;
        }

        var result = new Dictionary<string, object?>();

        // Busca as propriedades utilizando cache para reduzir o custo de Reflection em logs recorrentes.
        var properties = PropertyCache.GetOrAdd(type, currentType =>
            currentType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray());

        foreach (var property in properties)
        {
            if (property.GetCustomAttribute<LogIgnoreAttribute>() is not null)
            {
                continue;
            }

            if (property.GetCustomAttribute<LogSensitiveAttribute>() is not null)
            {
                result[property.Name] = Mask;
                continue;
            }

            // Proteção contra falhas de leitura em propriedades específicas.
            // Isso evita que uma exceção em um getter quebre o fluxo de logging da aplicação.
            try
            {
                var propertyValue = property.GetValue(value);
                result[property.Name] = Sanitize(propertyValue);
            }
            catch
            {
                result[property.Name] = "[Error reading property]";
            }
        }

        return result;
    }

    /// <summary>
    /// Indica se o tipo informado pode ser registrado diretamente no log sem inspeção por Reflection.
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(Guid)
            || type == typeof(TimeSpan);
    }
}
