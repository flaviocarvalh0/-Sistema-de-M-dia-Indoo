using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Platform.Entities
{
    [MetadataConfiguration("Parâmetros")]
    public class SystemParameter : BaseAuditableEntity
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Group { get; set; } = null!;
        public string DataType { get; set; } = null!;
    }
}
