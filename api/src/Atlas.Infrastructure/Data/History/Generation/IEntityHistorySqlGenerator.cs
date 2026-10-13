using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Generation;
internal interface IEntityHistorySqlGenerator
{
    string GenerateEnsureSchemaSql();

    string GenerateCreateHistoryTableSql(
        EntityHistoryDefinition definition);

    string GenerateCreateOrReplaceFunctionSql(
        EntityHistoryDefinition definition);

    string GenerateCreateTriggerSql(
        EntityHistoryDefinition definition);

    string GenerateDropTriggerSql(
        EntityHistoryDefinition definition);

    string GenerateCreateIndexesSql(
        EntityHistoryDefinition definition);

    string GenerateAddColumnSql(
        EntityHistoryDefinition definition,
        EntityHistoryColumnDefinition column);

    string GenerateCreateForeignKeySql(
        EntityHistoryDefinition definition);
}
