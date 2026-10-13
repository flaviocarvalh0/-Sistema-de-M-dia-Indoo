using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EntityHistory.Common;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data.History.Comparison;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Reading;

namespace Atlas.Infrastructure.Data.History;
internal sealed class EntityHistoryQueryService : IEntityHistoryQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly IEntityHistoryModelResolver _modelResolver;
    private readonly IEntityHistorySnapshotReader _snapshotReader;
    private readonly IEntityHistorySnapshotComparer _snapshotComparer;
    private readonly IIdentityService _identityService;

    public EntityHistoryQueryService(
        ApplicationDbContext context,
        IEntityHistoryModelResolver modelResolver,
        IEntityHistorySnapshotReader snapshotReader,
        IEntityHistorySnapshotComparer snapshotComparer,
        IIdentityService identityService)
    {
        _context = context;
        _modelResolver = modelResolver;
        _snapshotReader = snapshotReader;
        _snapshotComparer = snapshotComparer;
        _identityService = identityService;
    }

    public async Task<EntityHistoryDto> GetAsync(
        string routeKey,
        long recordId,
        long? beforeHistoryId = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        var metadata = await GetMetadataAsync(
            routeKey,
            cancellationToken);

        var definition = _modelResolver.Resolve(metadata);

        var batch = await _snapshotReader.ReadAsync(
            definition,
            recordId,
            beforeHistoryId,
            pageSize,
            cancellationToken);

        var comparisons = _snapshotComparer.Compare(
            definition,
            batch);

        // 1. Extrai todas as AuditInformations para coletar os UserIds
        var auditsAndComparisons = comparisons
            .Select(comparison => new
            {
                Comparison = comparison,
                Audit = ReadAuditInformation(definition, comparison)
            })
            .ToList();

        // 2. Coleta os IDs dos usuários convertendo a string para long (N+1 evitado)
        var userIds = auditsAndComparisons
            .Select(x => x.Audit.ChangedById)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => long.TryParse(id, out var parsed) ? parsed : (long?)null)
            .Where(parsedId => parsedId.HasValue)
            .Select(parsedId => parsedId!.Value)
            .Distinct();

        // 3. Resolve os nomes em uma única query SQL
        var userNamesMap = await _identityService.GetUserNamesAsync(userIds, cancellationToken);

        // 4. Mapeia as versões utilizando o dicionário resolvido
        var versions = auditsAndComparisons
            .Select(x => MapVersion(
                definition,
                x.Comparison,
                x.Audit,
                userNamesMap))
            .ToArray();

        return new EntityHistoryDto
        {
            RouteKey = metadata.RouteKey,
            EntityLabel = metadata.DisplayLabel,
            RecordId = recordId,
            HistoryEnabled = metadata.EnableHistory,
            Versions = versions,
            HasMore = batch.HasMore,
            NextCursor = batch.NextCursor
        };
    }

    private async Task<EntityMetadata> GetMetadataAsync(
        string routeKey,
        CancellationToken cancellationToken)
    {
        var normalizedRouteKey = routeKey.Trim();

        return await _context.EntityMetadata
            .AsNoTracking()
            .SingleOrDefaultAsync(
                metadata => metadata.RouteKey == normalizedRouteKey,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Nenhum metadado de entidade foi encontrado para a rota '{normalizedRouteKey}'.");
    }

    private static EntityHistoryVersionDto MapVersion(
        EntityHistoryDefinition definition,
        EntityHistoryVersionComparison comparison,
        AuditInformation audit,
        IReadOnlyDictionary<long, string> userNamesMap)
    {
        var changes = comparison.Changes
            .Select(change => MapChange(definition, change))
            .ToArray();

        // Tenta converter ChangedById (string) em long para buscar no dicionário
        string? changedByName = null;
        if (!string.IsNullOrWhiteSpace(audit.ChangedById) &&
            long.TryParse(audit.ChangedById, out var userId))
        {
            userNamesMap.TryGetValue(userId, out changedByName);
        }

        // Se não encontrou nome ou falhou no parse, usa a própria string de ID como fallback
        changedByName ??= audit.ChangedById;

        return new EntityHistoryVersionDto
        {
            HistoryId = comparison.HistoryId,
            CapturedAt = comparison.HistoryCreatedAt,
            ChangedAt = audit.ChangedAt,
            ChangedById = audit.ChangedById,
            ChangedByName = changedByName,
            IsInitialVersion = comparison.IsInitialVersion,
            Changes = changes
        };
    }

    private static EntityHistoryFieldChangeDto MapChange(
        EntityHistoryDefinition definition,
        EntityHistoryFieldChange change)
    {
        var column = definition.Columns.Single(
            item => string.Equals(
                item.ColumnName,
                change.ColumnName,
                StringComparison.OrdinalIgnoreCase));

        var property = definition.ClrType.GetProperty(
            column.PropertyName,
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.IgnoreCase);

        return new EntityHistoryFieldChangeDto
        {
            PropertyName = column.PropertyName,
            DisplayLabel = ResolveDisplayLabel(
                property,
                column.PropertyName),

            PreviousValue = change.PreviousValue,
            CurrentValue = change.CurrentValue,

            PreviousDisplayValue = FormatValue(
                change.PreviousValue,
                property?.PropertyType),

            CurrentDisplayValue = FormatValue(
                change.CurrentValue,
                property?.PropertyType)
        };
    }

    private static AuditInformation ReadAuditInformation(
        EntityHistoryDefinition definition,
        EntityHistoryVersionComparison comparison)
    {
        if (comparison.IsInitialVersion)
        {
            return new AuditInformation
            {
                ChangedAt = ReadDateTimeOffset(
                    definition,
                    comparison.Values,
                    "Created"),

                ChangedById = ReadString(
                    definition,
                    comparison.Values,
                    "CreatedBy")
            };
        }

        return new AuditInformation
        {
            ChangedAt =
                ReadDateTimeOffset(
                    definition,
                    comparison.Values,
                    "LastModified")
                ?? ReadDateTimeOffset(
                    definition,
                    comparison.Values,
                    "Created"),

            ChangedById =
                ReadString(
                    definition,
                    comparison.Values,
                    "LastModifiedBy")
                ?? ReadString(
                    definition,
                    comparison.Values,
                    "CreatedBy")
        };
    }

    private static object? ReadValue(
        EntityHistoryDefinition definition,
        IReadOnlyDictionary<string, object?> values,
        string propertyName)
    {
        var column = definition.Columns
            .FirstOrDefault(item =>
                string.Equals(
                    item.PropertyName,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));

        if (column is null)
        {
            return null;
        }

        values.TryGetValue(
            column.ColumnName,
            out var value);

        return value;
    }

    private static string? ReadString(
        EntityHistoryDefinition definition,
        IReadOnlyDictionary<string, object?> values,
        string propertyName)
    {
        return ReadValue(
            definition,
            values,
            propertyName)?.ToString();
    }

    private static DateTimeOffset? ReadDateTimeOffset(
        EntityHistoryDefinition definition,
        IReadOnlyDictionary<string, object?> values,
        string propertyName)
    {
        var value = ReadValue(
            definition,
            values,
            propertyName);

        return value switch
        {
            null => null,

            DateTimeOffset dateTimeOffset =>
                dateTimeOffset,

            DateTime dateTime when
                dateTime.Kind == DateTimeKind.Utc =>
                new DateTimeOffset(dateTime),

            DateTime dateTime =>
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        dateTime,
                        DateTimeKind.Utc)),

            _ => null
        };
    }

    private sealed record AuditInformation
    {
        public DateTimeOffset? ChangedAt { get; init; }

        public string? ChangedById { get; init; }
    }

    private static string ResolveDisplayLabel(
        PropertyInfo? property,
        string propertyName)
    {
        var displayAttribute =
            property?.GetCustomAttribute<DisplayAttribute>();

        var displayName =
            displayAttribute?.GetName();

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return SplitPascalCase(propertyName);
    }

    private static string SplitPascalCase(
        string value)
    {
        return Regex.Replace(
            value,
            @"(?<=[a-z0-9])([A-Z])",
            " $1");
    }

    private static string? FormatValue(
        object? value,
        Type? propertyType)
    {
        if (value is null)
        {
            return null;
        }

        var underlyingType =
            Nullable.GetUnderlyingType(propertyType ?? value.GetType())
            ?? propertyType
            ?? value.GetType();

        if (underlyingType.IsEnum)
        {
            return FormatEnum(
                underlyingType,
                value);
        }

        return value switch
        {
            bool boolean =>
                boolean ? "Sim" : "Não",

            DateTimeOffset dateTimeOffset =>
                dateTimeOffset.ToString(
                    "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.GetCultureInfo("pt-BR")),

            DateTime dateTime =>
                dateTime.ToString(
                    "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.GetCultureInfo("pt-BR")),

            decimal decimalValue =>
                decimalValue.ToString(
                    CultureInfo.GetCultureInfo("pt-BR")),

            byte[] =>
                "[conteúdo binário]",

            Array array =>
                string.Join(
                    ", ",
                    array.Cast<object?>()
                        .Select(item =>
                            item?.ToString() ?? string.Empty)),

            _ => Convert.ToString(
                value,
                CultureInfo.GetCultureInfo("pt-BR"))
        };
    }

    private static string FormatEnum(
        Type enumType,
        object value)
    {
        object enumValue;

        try
        {
            enumValue = value is string stringValue
                ? Enum.Parse(
                    enumType,
                    stringValue,
                    ignoreCase: true)
                : Enum.ToObject(
                    enumType,
                    value);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }

        var memberName =
            Enum.GetName(
                enumType,
                enumValue);

        if (memberName is null)
        {
            return value.ToString() ?? string.Empty;
        }

        var member =
            enumType.GetMember(memberName)
                .FirstOrDefault();

        return member?
                   .GetCustomAttribute<DisplayAttribute>()?
                   .GetName()
               ?? SplitPascalCase(memberName);
    }
}
