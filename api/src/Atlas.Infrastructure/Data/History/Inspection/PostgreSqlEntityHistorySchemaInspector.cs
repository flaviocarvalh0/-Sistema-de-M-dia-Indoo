using System.Data;
using Atlas.Infrastructure.Data.History.Generation;
using Atlas.Infrastructure.Data.History.Modeling;
using Npgsql;

namespace Atlas.Infrastructure.Data.History.Inspection;

/// <summary>
/// Consulta os catálogos do PostgreSQL para descobrir o estado atual
/// da infraestrutura histórica de uma entidade.
/// </summary>
internal sealed class PostgreSqlEntityHistorySchemaInspector
    : IEntityHistorySchemaInspector
{
    private readonly ApplicationDbContext _context;

    public PostgreSqlEntityHistorySchemaInspector(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EntityHistoryInfrastructureState> InspectAsync(
        EntityHistoryDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var connection = _context.Database.GetDbConnection();

        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var schemaExists = await SchemaExistsAsync(
                connection,
                definition.HistorySchema,
                cancellationToken);

            var tableExists = schemaExists &&
                await TableExistsAsync(
                    connection,
                    definition.HistorySchema,
                    definition.HistoryTable,
                    cancellationToken);

            var columns = tableExists
                ? await GetColumnsAsync(
                    connection,
                    definition.HistorySchema,
                    definition.HistoryTable,
                    cancellationToken)
                : [];

            var functionExists = schemaExists &&
                await FunctionExistsAsync(
                    connection,
                    definition.HistorySchema,
                    BuildFunctionName(definition),
                    cancellationToken);

            var triggerExists = await TriggerExistsAsync(
                connection,
                definition.SourceSchema,
                definition.SourceTable,
                BuildTriggerName(definition),
                cancellationToken);

            var indexExists = tableExists &&
                await IndexExistsAsync(
                    connection,
                    definition.HistorySchema,
                    BuildIndexName(definition),
                    cancellationToken);

            var foreignKeyExists = tableExists &&
                await ForeignKeyExistsAsync(
                    connection,
                    definition.HistorySchema,
                    definition.HistoryTable,
                    BuildForeignKeyName(definition),
                    cancellationToken);

            return new EntityHistoryInfrastructureState
            {
                SchemaExists = schemaExists,
                TableExists = tableExists,
                FunctionExists = functionExists,
                TriggerExists = triggerExists,
                IndexExists = indexExists,
                ForeignKeyExists = foreignKeyExists,
                Columns = columns
            };
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> SchemaExistsAsync(
        System.Data.Common.DbConnection connection,
        string schemaName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_catalog.pg_namespace
                WHERE nspname = @schema_name
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [new NpgsqlParameter("schema_name", schemaName)],
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_catalog.pg_class AS relation
                INNER JOIN pg_catalog.pg_namespace AS namespace
                    ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = @schema_name
                  AND relation.relname = @table_name
                  AND relation.relkind IN ('r', 'p')
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [
                new NpgsqlParameter("schema_name", schemaName),
                new NpgsqlParameter("table_name", tableName)
            ],
            cancellationToken);
    }

    private static async Task<IReadOnlyCollection<EntityHistoryDatabaseColumn>>
        GetColumnsAsync(
            System.Data.Common.DbConnection connection,
            string schemaName,
            string tableName,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                column_name,
                data_type,
                udt_name,
                is_nullable,
                character_maximum_length,
                numeric_precision,
                numeric_scale
            FROM information_schema.columns
            WHERE table_schema = @schema_name
              AND table_name = @table_name
            ORDER BY ordinal_position;
            """;

        await using var command = connection.CreateCommand();

        command.CommandText = sql;

        command.Parameters.Add(
            new NpgsqlParameter("schema_name", schemaName));

        command.Parameters.Add(
            new NpgsqlParameter("table_name", tableName));

        var columns = new List<EntityHistoryDatabaseColumn>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var dataType = reader.GetString(
                reader.GetOrdinal("data_type"));

            var udtName = reader.GetString(
                reader.GetOrdinal("udt_name"));

            var storeType = BuildStoreType(
                reader,
                dataType,
                udtName);

            columns.Add(new EntityHistoryDatabaseColumn
            {
                Name = reader.GetString(
                    reader.GetOrdinal("column_name")),

                StoreType = storeType,

                IsNullable = string.Equals(
                    reader.GetString(
                        reader.GetOrdinal("is_nullable")),
                    "YES",
                    StringComparison.OrdinalIgnoreCase)
            });
        }

        return columns;
    }

    private static async Task<bool> FunctionExistsAsync(
        System.Data.Common.DbConnection connection,
        string schemaName,
        string functionName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_catalog.pg_proc AS procedure
                INNER JOIN pg_catalog.pg_namespace AS namespace
                    ON namespace.oid = procedure.pronamespace
                WHERE namespace.nspname = @schema_name
                  AND procedure.proname = @function_name
                  AND procedure.pronargs = 0
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [
                new NpgsqlParameter("schema_name", schemaName),
                new NpgsqlParameter("function_name", functionName)
            ],
            cancellationToken);
    }

    private static async Task<bool> TriggerExistsAsync(
        System.Data.Common.DbConnection connection,
        string sourceSchema,
        string sourceTable,
        string triggerName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_catalog.pg_trigger AS trigger
                INNER JOIN pg_catalog.pg_class AS relation
                    ON relation.oid = trigger.tgrelid
                INNER JOIN pg_catalog.pg_namespace AS namespace
                    ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = @schema_name
                  AND relation.relname = @table_name
                  AND trigger.tgname = @trigger_name
                  AND NOT trigger.tgisinternal
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [
                new NpgsqlParameter("schema_name", sourceSchema),
                new NpgsqlParameter("table_name", sourceTable),
                new NpgsqlParameter("trigger_name", triggerName)
            ],
            cancellationToken);
    }

    private static async Task<bool> IndexExistsAsync(
        System.Data.Common.DbConnection connection,
        string schemaName,
        string indexName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM pg_catalog.pg_class AS index_relation
                INNER JOIN pg_catalog.pg_namespace AS namespace
                    ON namespace.oid = index_relation.relnamespace
                WHERE namespace.nspname = @schema_name
                  AND index_relation.relname = @index_name
                  AND index_relation.relkind = 'i'
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [
                new NpgsqlParameter("schema_name", schemaName),
                new NpgsqlParameter("index_name", indexName)
            ],
            cancellationToken);
    }

    private static async Task<bool> ForeignKeyExistsAsync(
        System.Data.Common.DbConnection connection,
        string schemaName,
        string tableName,
        string constraintName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM information_schema.table_constraints
                WHERE constraint_schema = @schema_name
                  AND table_name = @table_name
                  AND constraint_name = @constraint_name
                  AND constraint_type = 'FOREIGN KEY'
            );
            """;

        return await ExecuteBooleanAsync(
            connection,
            sql,
            [
                new NpgsqlParameter("schema_name", schemaName),
                new NpgsqlParameter("table_name", tableName),
                new NpgsqlParameter("constraint_name", constraintName)
            ],
            cancellationToken);
    }

    private static async Task<bool> ExecuteBooleanAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        IReadOnlyCollection<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        return result is true;
    }

    private static string BuildStoreType(
        System.Data.Common.DbDataReader reader,
        string dataType,
        string udtName)
    {
        var lengthOrdinal = reader.GetOrdinal(
            "character_maximum_length");

        var precisionOrdinal = reader.GetOrdinal(
            "numeric_precision");

        var scaleOrdinal = reader.GetOrdinal(
            "numeric_scale");

        if (string.Equals(
                dataType,
                "character varying",
                StringComparison.OrdinalIgnoreCase) &&
            !reader.IsDBNull(lengthOrdinal))
        {
            return $"character varying({reader.GetInt32(lengthOrdinal)})";
        }

        if (string.Equals(
                dataType,
                "numeric",
                StringComparison.OrdinalIgnoreCase) &&
            !reader.IsDBNull(precisionOrdinal) &&
            !reader.IsDBNull(scaleOrdinal))
        {
            var precision = Convert.ToInt32(
                reader.GetValue(precisionOrdinal));

            var scale = Convert.ToInt32(
                reader.GetValue(scaleOrdinal));

            return $"numeric({precision},{scale})";
        }

        return dataType switch
        {
            "ARRAY" => udtName,
            "USER-DEFINED" => udtName,
            _ => dataType
        };
    }

    private static string BuildFunctionName(
        EntityHistoryDefinition definition)
    {
        return EntityHistoryIdentifierBuilder.Build(
            "capture",
            definition.HistoryTable);
    }

    private static string BuildTriggerName(
        EntityHistoryDefinition definition)
    {
        return EntityHistoryIdentifierBuilder.Build(
            "trg",
            definition.HistoryTable);
    }

    private static string BuildIndexName(
        EntityHistoryDefinition definition)
    {
        return EntityHistoryIdentifierBuilder.Build(
            "ix",
            definition.HistoryTable,
            "record_id",
            "history_created_at");
    }

    private static string BuildForeignKeyName(
        EntityHistoryDefinition definition)
    {
        return EntityHistoryIdentifierBuilder.Build(
            "fk",
            definition.HistoryTable,
            definition.SourceTable);
    }
}
