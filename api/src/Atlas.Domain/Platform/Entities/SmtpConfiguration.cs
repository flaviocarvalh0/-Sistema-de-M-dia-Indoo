using Atlas.Shared.Observability;

namespace Atlas.Domain.Platform.Entities;

public class SmtpConfiguration : BaseAuditableEntity
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public bool EnableSsl { get; set; }

    [LogSensitive]
    public string UserName { get; set; } = null!;

    [LogSensitive]
    public string Password { get; set; } = null!;

    [LogSensitive]
    public string SenderEmail { get; set; } = null!;
    public string? SenderName { get; set; }
}
