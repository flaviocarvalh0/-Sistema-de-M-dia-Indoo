namespace Atlas.Domain.Common;

public class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset Created { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public long? LastModifiedBy { get; set; }
}
