namespace Atlas.Application.Platform.Smtp.Queries.GetSmtpConfiguration;

public class SmtpConfigurationDto
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public bool EnableSsl { get; set; }

    [LogSensitive]
    public string UserName { get; set; } = null!;

    public string SenderEmail { get; set; } = null!;

    [LogSensitive]
    public string? SenderName { get; set; }
}
