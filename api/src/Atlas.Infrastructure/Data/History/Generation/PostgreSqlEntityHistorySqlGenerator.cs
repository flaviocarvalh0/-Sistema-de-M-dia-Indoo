using System.Text;
using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Generation;
/// <summary>
/// Gera os comandos PostgreSQL necessários para provisionar
/// a infraestrutura histórica de uma entidade.
/// </summary>
internal sealed class PostgreSqlEntityHistorySqlGenerator
    : IEntityHistorySqlGenerator
{
    public string GenerateEnsureSchemaSql()
    {
        return """
            CREATE SCHEMA IF NOT EXISTS "history";
            """;
    }

    public string GenerateCreateHistoryTableSql(
        EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var historySchema = Quote(definition.HistorySchema);
        var historyTable = Quote(definition.HistoryTable);
        var sourceSchema = Quote(definition.SourceSchema);
        var sourceTable = Quote(definition.SourceTable);
        var primaryKeyColumn = Quote(definition.PrimaryKeyColumn);

        var columns = definition.Columns
            .Where(column => !column.IsPrimaryKey)
            .Select(column =>
                $"    {Quote(column.ColumnName)} {column.StoreType} NULL")
            .ToList();

        var constraintName = BuildIdentifier(
            "fk",
            definition.HistoryTable,
            definition.SourceTable);

        var builder = new StringBuilder();

        builder.AppendLine(
            $"CREATE TABLE IF NOT EXISTS {historySchema}.{historyTable}");
        builder.AppendLine("(");
        builder.AppendLine(
            "    \"history_id\" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,");
        builder.AppendLine(
            $"    \"record_id\" {definition.PrimaryKeyStoreType} NOT NULL,");
        builder.AppendLine(
            "    \"history_created_at\" timestamp with time zone NOT NULL DEFAULT clock_timestamp(),");

        if (columns.Count > 0)
        {
            builder.AppendLine(string.Join("," + Environment.NewLine, columns) + ",");
        }

        builder.AppendLine(
            $"    CONSTRAINT {Quote(constraintName)}");
        builder.AppendLine(
            $"        FOREIGN KEY (\"record_id\")");
        builder.AppendLine(
            $"        REFERENCES {sourceSchema}.{sourceTable} ({primaryKeyColumn})");
        builder.AppendLine(
            "        ON DELETE CASCADE");
        builder.AppendLine(");");

        return builder.ToString();
    }

    public string GenerateCreateOrReplaceFunctionSql(
        EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var functionName = BuildIdentifier(
            "capture",
            definition.HistoryTable);

        var historySchema = Quote(definition.HistorySchema);
        var historyTable = Quote(definition.HistoryTable);
        var quotedFunctionName = Quote(functionName);
        var sourceTableLiteral = QuoteLiteral(definition.SourceTable);

        var snapshotColumns = definition.Columns
            .Where(column => !column.IsPrimaryKey)
            .ToList();

        var insertColumns = new List<string>
        {
            "\"record_id\""
        };

        insertColumns.AddRange(
            snapshotColumns.Select(column =>
                Quote(column.ColumnName)));

        var values = new List<string>
        {
            $"NEW.{Quote(definition.PrimaryKeyColumn)}"
        };

        values.AddRange(
            snapshotColumns.Select(column =>
                $"NEW.{Quote(column.ColumnName)}"));

        return $$"""
            CREATE OR REPLACE FUNCTION {{historySchema}}.{{quotedFunctionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM "platform"."entity_metadata" AS metadata
                    WHERE metadata."table_name" = {{sourceTableLiteral}}
                      AND metadata."enable_history" = TRUE
                )
                THEN
                    RETURN NEW;
                END IF;

                INSERT INTO {{historySchema}}.{{historyTable}}
                (
                    {{string.Join("," + Environment.NewLine + "        ", insertColumns)}}
                )
                VALUES
                (
                    {{string.Join("," + Environment.NewLine + "        ", values)}}
                );

                RETURN NEW;
            END;
            $function$;
            """;
    }

    public string GenerateCreateTriggerSql(
        EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var triggerName = BuildIdentifier(
            "trg",
            definition.HistoryTable);

        var functionName = BuildIdentifier(
            "capture",
            definition.HistoryTable);

        return $$"""
            DROP TRIGGER IF EXISTS {{Quote(triggerName)}}
            ON {{Quote(definition.SourceSchema)}}.{{Quote(definition.SourceTable)}};

            CREATE TRIGGER {{Quote(triggerName)}}
            AFTER INSERT OR UPDATE
            ON {{Quote(definition.SourceSchema)}}.{{Quote(definition.SourceTable)}}
            FOR EACH ROW
            EXECUTE FUNCTION {{Quote(definition.HistorySchema)}}.{{Quote(functionName)}}();
            """;
    }

    public string GenerateDropTriggerSql(
        EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var triggerName = BuildIdentifier(
            "trg",
            definition.HistoryTable);

        return $$"""
            DROP TRIGGER IF EXISTS {{Quote(triggerName)}}
            ON {{Quote(definition.SourceSchema)}}.{{Quote(definition.SourceTable)}};
            """;
    }

    private static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(
                "O identificador PostgreSQL não pode ser vazio.",
                nameof(identifier));
        }

        // Remove aspas duplas das pontas (caso já existam) e limpa aspas duplicadas
        var unquoted = identifier.Trim('"').Replace("\"\"", "\"", StringComparison.Ordinal);

        // Aplica a regra oficial do PostgreSQL: escapa aspas internas (" -> "") e envolve por aspas duplas
        return $"\"{unquoted.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string BuildIdentifier(
        params string[] parts)
    {
        var identifier = string.Join(
            "_",
            parts.Where(part => !string.IsNullOrWhiteSpace(part)));

        // PostgreSQL limita identificadores a 63 bytes.
        if (identifier.Length <= 63)
        {
            return identifier;
        }

        var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(identifier)))
            .ToLowerInvariant()[..12];

        var availableLength = 63 - hash.Length - 1;

        return $"{identifier[..availableLength]}_{hash}";
    }

    public string GenerateCreateIndexesSql(
    EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var indexName = BuildIdentifier(
            "ix",
            definition.HistoryTable,
            "record_id",
            "history_created_at");

        return $$"""
            CREATE INDEX IF NOT EXISTS {{Quote(indexName)}}
            ON {{Quote(definition.HistorySchema)}}.{{Quote(definition.HistoryTable)}}
            (
                "record_id",
                "history_created_at" DESC,
                "history_id" DESC
            );
            """;
    }


    public string GenerateAddColumnSql(
        EntityHistoryDefinition definition,
        EntityHistoryColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(column);

        if (column.IsPrimaryKey)
        {
            throw new InvalidOperationException(
                "A chave primária original não deve ser adicionada como " +
                "coluna de snapshot.");
        }

        return $$"""
            ALTER TABLE {{Quote(definition.HistorySchema)}}.{{Quote(definition.HistoryTable)}}
            ADD COLUMN IF NOT EXISTS {{Quote(column.ColumnName)}} {{column.StoreType}} NULL;
            """;
    }

    public string GenerateCreateForeignKeySql(
        EntityHistoryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var constraintName =
            EntityHistoryIdentifierBuilder.Build(
                "fk",
                definition.HistoryTable,
                definition.SourceTable);

        var historySchema = Quote(definition.HistorySchema);
        var historyTable = Quote(definition.HistoryTable);
        var quotedConstraintName = Quote(constraintName);

        var sourceSchema = Quote(definition.SourceSchema);
        var sourceTable = Quote(definition.SourceTable);
        var primaryKeyColumn = Quote(definition.PrimaryKeyColumn);

        var schemaLiteral = QuoteLiteral(definition.HistorySchema);
        var tableLiteral = QuoteLiteral(definition.HistoryTable);
        var constraintLiteral = QuoteLiteral(constraintName);

        return $$"""
            DO $block$
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM information_schema.table_constraints
                    WHERE constraint_schema = {{schemaLiteral}}
                      AND table_name = {{tableLiteral}}
                      AND constraint_name = {{constraintLiteral}}
                      AND constraint_type = 'FOREIGN KEY'
                )
                THEN
                    ALTER TABLE {{historySchema}}.{{historyTable}}
                    ADD CONSTRAINT {{quotedConstraintName}}
                    FOREIGN KEY ("record_id")
                    REFERENCES {{sourceSchema}}.{{sourceTable}}
                        ({{primaryKeyColumn}})
                    ON DELETE CASCADE;
                END IF;
            END;
            $block$;
            """;
    }

    private static string QuoteLiteral(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
