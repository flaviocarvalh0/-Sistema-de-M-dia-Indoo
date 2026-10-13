namespace Atlas.Infrastructure.Data.History.Planning;

/// <summary>
/// Identifica uma operação necessária para deixar a infraestrutura
/// histórica compatível com a definição atual da entidade.
/// </summary>
internal enum EntityHistoryProvisioningAction
{
    CreateSchema,
    CreateTable,
    AddColumn,
    CreateIndex,
    CreateForeignKey,
    CreateOrReplaceFunction,
    CreateTrigger
}
