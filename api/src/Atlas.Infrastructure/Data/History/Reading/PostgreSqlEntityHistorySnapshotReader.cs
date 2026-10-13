using System.Data;
using System.Data.Common;
using System.Text;
using Atlas.Infrastructure.Data.History.Modeling;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Atlas.Infrastructure.Data.History.Reading;

/// <summary>
/// Lê snapshots diretamente das tabelas históricas provisionadas
/// pelo engenho do Atlas.
/// </summary>
internal sealed class PostgreSqlEntityHistorySnapshotReader
    : IEntityHistorySnapshotReader
{
    private const int DefaultPageSize = 100;
    private const int MaximumPageSize = 200;

    private readonly ApplicationDbContext _context;

    public PostgreSqlEntityHistorySnapshotReader(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EntityHistorySnapshotBatch> ReadAsync(
        EntityHistoryDefinition definition,
        long recordId,
        long? beforeHistoryId = null,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (recordId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordId),
                recordId,
                "O identificador do registro deve ser maior que zero.");
        }

        if (beforeHistoryId is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(beforeHistoryId),
                beforeHistoryId,
                "O cursor do histórico deve ser maior que zero.");
        }

        if (pageSize <= 0 || pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"O tamanho da página deve estar entre 1 e {MaximumPageSize}.");
        }

        var connection = _context.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await ReadSnapshotsAsync(
                connection,
                definition,
                recordId,
                beforeHistoryId,
                pageSize,
                cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<EntityHistorySnapshotBatch> ReadSnapshotsAsync(
        DbConnection connection,
        EntityHistoryDefinition definition,
        long recordId,
        long? beforeHistoryId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = BuildSql(
            definition,
            includeCursor: beforeHistoryId.HasValue);

        command.Parameters.Add(
            new NpgsqlParameter<long>(
                "record_id",
                recordId));

        command.Parameters.Add(
            new NpgsqlParameter<int>(
                "limit",
                pageSize + 1));

        if (beforeHistoryId.HasValue)
        {
            command.Parameters.Add(
                new NpgsqlParameter<long>(
                    "before_history_id",
                    beforeHistoryId.Value));
        }

        var currentTransaction =
            _context.Database.CurrentTransaction;

        if (currentTransaction is not null)
        {
            command.Transaction =
                currentTransaction.GetDbTransaction();
        }

        var snapshots = new List<EntityHistorySnapshot>(
            capacity: pageSize + 1);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(
                MapSnapshot(
                    reader,
                    definition));
        }

        var hasAdditionalSnapshot =
            snapshots.Count > pageSize;

        EntityHistorySnapshot? comparisonBaseline = null;

        if (hasAdditionalSnapshot)
        {
            comparisonBaseline = snapshots[^1];
            snapshots.RemoveAt(snapshots.Count - 1);
        }

        var nextCursor = hasAdditionalSnapshot && snapshots.Count > 0
            ? (long?)snapshots[^1].HistoryId
            : null;

        return new EntityHistorySnapshotBatch
        {
            Snapshots = snapshots,
            ComparisonBaseline = comparisonBaseline,
            HasMore = hasAdditionalSnapshot,
            NextCursor = nextCursor
        };
    }

    private static string BuildSql(
        EntityHistoryDefinition definition,
        bool includeCursor)
    {
        var historySchema =
            Quote(definition.HistorySchema);

        var historyTable =
            Quote(definition.HistoryTable);

        var snapshotColumns = definition.Columns
            .Where(column => !column.IsPrimaryKey)
            .Select(column => Quote(column.ColumnName))
            .ToArray();

        var selectColumns = new List<string>
        {
            "\"history_id\"",
            "\"record_id\"",
            "\"history_created_at\""
        };

        selectColumns.AddRange(snapshotColumns);

        var builder = new StringBuilder();

        builder.AppendLine("SELECT");
        builder.AppendLine(
            $"    {string.Join("," + Environment.NewLine + "    ", selectColumns)}");
        builder.AppendLine(
            $"FROM {historySchema}.{historyTable}");
        builder.AppendLine(
            "WHERE \"record_id\" = @record_id");

        if (includeCursor)
        {
            builder.AppendLine(
                "  AND \"history_id\" < @before_history_id");
        }

        builder.AppendLine(
            "ORDER BY \"history_id\" DESC");
        builder.AppendLine(
            "LIMIT @limit;");

        return builder.ToString();
    }

    private static EntityHistorySnapshot MapSnapshot(
        DbDataReader reader,
        EntityHistoryDefinition definition)
    {
        var values = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var column in definition.Columns
                     .Where(column => !column.IsPrimaryKey))
        {
            var ordinal =
                reader.GetOrdinal(column.ColumnName);

            values[column.ColumnName] =
                reader.IsDBNull(ordinal)
                    ? null
                    : reader.GetValue(ordinal);
        }

        return new EntityHistorySnapshot
        {
            HistoryId = reader.GetInt64(
                reader.GetOrdinal("history_id")),

            RecordId = ReadInt64(
                reader,
                "record_id"),

            HistoryCreatedAt = ReadDateTimeOffset(
                reader,
                "history_created_at"),

            Values = values
        };
    }

    private static long ReadInt64(
        DbDataReader reader,
        string columnName)
    {
        var value = reader.GetValue(
            reader.GetOrdinal(columnName));

        return Convert.ToInt64(value);
    }

    private static DateTimeOffset ReadDateTimeOffset(
        DbDataReader reader,
        string columnName)
    {
        var value = reader.GetValue(
            reader.GetOrdinal(columnName));

        return value switch
        {
            DateTimeOffset dateTimeOffset =>
                dateTimeOffset,

            DateTime dateTime when dateTime.Kind ==
                                   DateTimeKind.Utc =>
                new DateTimeOffset(dateTime),

            DateTime dateTime =>
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        dateTime,
                        DateTimeKind.Utc)),

            _ => throw new InvalidOperationException(
                $"A coluna '{columnName}' retornou o tipo inesperado " +
                $"'{value.GetType().FullName}'.")
        };
    }

    private static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(
                "O identificador PostgreSQL não pode ser vazio.",
                nameof(identifier));
        }

        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
