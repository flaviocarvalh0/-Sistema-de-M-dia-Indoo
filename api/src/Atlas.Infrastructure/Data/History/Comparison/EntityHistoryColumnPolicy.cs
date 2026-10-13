using Atlas.Infrastructure.Data.History.Modeling;

namespace Atlas.Infrastructure.Data.History.Comparison;
internal static class EntityHistoryColumnPolicy
{
    private static readonly HashSet<string> AuditPropertyNames =
        new(
            [
                "Created",
                "CreatedBy",
                "LastModified",
                "LastModifiedBy"
            ],
            StringComparer.OrdinalIgnoreCase);

    public static bool IsDisplayedAsFieldChange(
        EntityHistoryColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return !column.IsPrimaryKey &&
               !AuditPropertyNames.Contains(
                   column.PropertyName);
    }

    public static bool IsAuditProperty(
        string propertyName)
    {
        return AuditPropertyNames.Contains(
            propertyName);
    }
}
