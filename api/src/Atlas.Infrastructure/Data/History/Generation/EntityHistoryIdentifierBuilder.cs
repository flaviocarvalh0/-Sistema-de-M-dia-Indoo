using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Data.History.Generation;

/// <summary>
/// Produz identificadores PostgreSQL determinísticos respeitando
/// o limite máximo de 63 bytes.
/// </summary>
internal static class EntityHistoryIdentifierBuilder
{
    private const int PostgreSqlIdentifierMaxLength = 63;
    private const int HashLength = 12;

    public static string Build(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var normalizedParts = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim().ToLowerInvariant())
            .ToArray();

        if (normalizedParts.Length == 0)
        {
            throw new ArgumentException(
                "Ao menos uma parte válida deve ser informada.",
                nameof(parts));
        }

        var identifier = string.Join("_", normalizedParts);

        if (Encoding.UTF8.GetByteCount(identifier) <=
            PostgreSqlIdentifierMaxLength)
        {
            return identifier;
        }

        var hash = Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(identifier)))
            .ToLowerInvariant()[..HashLength];

        var suffix = $"_{hash}";
        var availableBytes =
            PostgreSqlIdentifierMaxLength -
            Encoding.UTF8.GetByteCount(suffix);

        var prefix = TruncateByUtf8Bytes(
            identifier,
            availableBytes);

        return $"{prefix}{suffix}";
    }

    private static string TruncateByUtf8Bytes(
        string value,
        int maxBytes)
    {
        var builder = new StringBuilder();
        var currentBytes = 0;

        foreach (var character in value)
        {
            var characterBytes =
                Encoding.UTF8.GetByteCount(
                    character.ToString());

            if (currentBytes + characterBytes > maxBytes)
            {
                break;
            }

            builder.Append(character);
            currentBytes += characterBytes;
        }

        return builder
            .ToString()
            .TrimEnd('_');
    }
}
