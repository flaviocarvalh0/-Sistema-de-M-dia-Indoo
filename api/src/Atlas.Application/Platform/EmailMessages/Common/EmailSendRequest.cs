namespace Atlas.Application.Platform.EmailMessages.Common;

/// <summary>
/// Representa uma mensagem completamente materializada
/// e pronta para entrega por um mecanismo de transporte.
/// </summary>
/// <remarks>
/// Este contrato não representa uma entidade persistida e não
/// possui dependência de Entity Framework Core.
/// </remarks>
public sealed record EmailSendRequest
{
    public string Subject { get; init; } = null!;

    [LogIgnore]
    public string HtmlBody { get; init; } = null!;

    public IReadOnlyCollection<EmailSendRecipient> Recipients { get; init; } =
        Array.Empty<EmailSendRecipient>();

    public IReadOnlyCollection<EmailSendAttachment> Attachments { get; init; } =
        Array.Empty<EmailSendAttachment>();
}

/// <summary>
/// Destinatário materializado para o transporte.
/// </summary>
public sealed record EmailSendRecipient
{
    [LogSensitive]
    public string? Name { get; init; }

    [LogSensitive]
    public string Email { get; init; } = null!;
    public EmailRecipientType Type { get; init; }
}

/// <summary>
/// Conteúdo materializado para composição MIME.
/// </summary>
public sealed record EmailSendAttachment
{
    public string FileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;

    [LogIgnore]
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public EmailAttachmentDisposition Disposition { get; init; }
    public string? ContentId { get; init; }
}
