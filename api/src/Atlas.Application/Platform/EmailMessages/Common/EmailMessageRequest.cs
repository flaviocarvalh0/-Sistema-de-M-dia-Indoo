namespace Atlas.Application.Platform.EmailMessages.Common;

/// <summary>
/// Representa a solicitação pública para criação de uma mensagem
/// na fila técnica de e-mails da Atlas Platform.
/// </summary>
public sealed record EmailMessageRequest
{
    /// <summary>
    /// Assunto da mensagem.
    /// </summary>
    public string Subject { get; init; } = null!;

    /// <summary>
    /// Corpo HTML da mensagem.
    /// </summary>
    [LogIgnore]
    public string HtmlBody { get; init; } = null!;

    /// <summary>
    /// Destinatários técnicos da mensagem.
    /// </summary>
    public IReadOnlyCollection<EmailRecipientRequest> Recipients { get; init; } =
        Array.Empty<EmailRecipientRequest>();

    /// <summary>
    /// Anexos e recursos inline da mensagem.
    /// </summary>
    public IReadOnlyCollection<EmailAttachmentRequest> Attachments { get; init; } =
        Array.Empty<EmailAttachmentRequest>();

    /// <summary>
    /// Tipo lógico opcional da entidade relacionada.
    /// </summary>
    public string? RelatedEntityType { get; init; }

    /// <summary>
    /// Identificador lógico opcional da entidade relacionada.
    /// </summary>
    public string? RelatedEntityId { get; init; }
}

/// <summary>
/// Representa um destinatário informado por um consumidor
/// da infraestrutura de e-mail.
/// </summary>
public sealed record EmailRecipientRequest
{
    [LogSensitive]
    public string? Name { get; init; }
    [LogSensitive]
    public string Email { get; init; } = null!;
    public EmailRecipientType Type { get; init; }
}

/// <summary>
/// Representa um conteúdo já persistido em FileContent
/// que deverá participar da mensagem.
/// </summary>
public sealed record EmailAttachmentRequest
{
    public long FileContentId { get; init; }
    public EmailAttachmentDisposition Disposition { get; init; }
    public string? FileName { get; init; }
    public string? ContentId { get; init; }
}
