namespace Atlas.Infrastructure.Data.History.Planning;
internal sealed class EntityHistorySchemaIncompatibleException
    : InvalidOperationException
{
    public EntityHistorySchemaIncompatibleException(
        string message)
        : base(message)
    {
    }
}
