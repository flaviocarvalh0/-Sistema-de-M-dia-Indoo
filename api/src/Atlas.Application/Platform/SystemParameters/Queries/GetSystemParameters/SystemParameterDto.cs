namespace Atlas.Application.Platform.SystemParameters.Queries.GetSystemParameters;

public class SystemParameterDto
{
    public long Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Group { get; set; } = null!;
    public string DataType { get; set; } = null!;


    public DateTimeOffset Created { get; set; }
    public string? CreatedByName { get; init; }
    public long? CreatedBy { get; set; }

    public DateTimeOffset LastModified { get; set; }
    public long? LastModifiedBy { get; set; }
    public string? LastModifiedByName { get; init; }

    public uint Version { get; set; }
}
