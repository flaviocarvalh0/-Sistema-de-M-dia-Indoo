using Atlas.Domain.Platform.Enums;
using Atlas.Shared.Observability;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa uma mensagem de e-mail persistida na fila técnica
/// da Atlas Platform.
/// </summary>
/// <remarks>
/// A entidade atua como agregado responsável pela composição
/// da mensagem, destinatários, anexos e transições do fluxo
/// assíncrono de envio.
/// </remarks>
public class EmailMessage : BaseAuditableEntity
{
    private readonly List<EmailMessageRecipient> _recipients = [];
    private readonly List<EmailMessageAttachment> _attachments = [];

    private EmailMessage()
    {
    }

    public EmailMessage(
        string subject,
        string htmlBody,
        string? relatedEntityType = null,
        string? relatedEntityId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);

        Subject = subject.Trim();
        HtmlBody = htmlBody;
        RelatedEntityType = NormalizeOptional(relatedEntityType);
        RelatedEntityId = NormalizeOptional(relatedEntityId);

        Status = EmailMessageStatus.Pending;
        RetryCount = 0;
    }

    /// <summary>
    /// Assunto da mensagem.
    /// </summary>
    public string Subject { get; private set; } = null!;

    /// <summary>
    /// Corpo HTML da mensagem.
    /// </summary>
    [LogIgnore]
    public string HtmlBody { get; private set; } = null!;

    /// <summary>
    /// Situação atual da mensagem no fluxo técnico de envio.
    /// </summary>
    public EmailMessageStatus Status { get; private set; }

    /// <summary>
    /// Mensagem técnica relacionada à última falha,
    /// quando existente.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Quantidade de tentativas de envio já executadas.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Data e hora UTC do envio concluído com sucesso.
    /// </summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>
    /// Data e hora UTC da última tentativa de envio.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>
    /// Tipo lógico da entidade relacionada à mensagem,
    /// quando houver.
    /// </summary>
    public string? RelatedEntityType { get; private set; }

    /// <summary>
    /// Identificador lógico da entidade relacionada,
    /// quando houver.
    /// </summary>
    public string? RelatedEntityId { get; private set; }

    /// <summary>
    /// Destinatários congelados da mensagem.
    /// </summary>
    public IReadOnlyCollection<EmailMessageRecipient> Recipients =>
        _recipients;

    /// <summary>
    /// Anexos e recursos inline congelados da mensagem.
    /// </summary>
    public IReadOnlyCollection<EmailMessageAttachment> Attachments =>
        _attachments;

    /// <summary>
    /// Adiciona um destinatário à mensagem.
    /// </summary>
    public void AddRecipient(
        string email,
        EmailRecipientType type,
        string? name = null)
    {
        EnsureCompositionCanBeChanged();

        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = NormalizeEmail(email);

        if (_recipients.Any(recipient =>
                recipient.NormalizedEmail == normalizedEmail))
        {
            throw new InvalidOperationException(
                $"O endereço de e-mail '{email.Trim()}' já foi adicionado à mensagem.");
        }

        var recipient = new EmailMessageRecipient(
            this,
            email,
            type,
            name);

        _recipients.Add(recipient);
    }

    /// <summary>
    /// Adiciona um arquivo à mensagem como anexo convencional.
    /// </summary>
    public void AddAttachment(
        FileContent fileContent,
        string? fileName = null)
    {
        EnsureCompositionCanBeChanged();

        ArgumentNullException.ThrowIfNull(fileContent);

        var attachment = new EmailMessageAttachment(
            this,
            fileContent,
            EmailAttachmentDisposition.Attachment,
            fileName);

        _attachments.Add(attachment);
    }

    /// <summary>
    /// Adiciona um recurso inline à mensagem.
    /// </summary>
    public void AddInlineResource(
        FileContent fileContent,
        string contentId,
        string? fileName = null)
    {
        EnsureCompositionCanBeChanged();

        ArgumentNullException.ThrowIfNull(fileContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);

        var normalizedContentId = contentId.Trim();

        if (_attachments.Any(attachment =>
                attachment.Disposition == EmailAttachmentDisposition.Inline &&
                string.Equals(
                    attachment.ContentId,
                    normalizedContentId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"O Content-ID '{normalizedContentId}' já foi adicionado à mensagem.");
        }

        var attachment = new EmailMessageAttachment(
            this,
            fileContent,
            EmailAttachmentDisposition.Inline,
            fileName,
            normalizedContentId);

        _attachments.Add(attachment);
    }

    /// <summary>
    /// Garante que a mensagem possui composição mínima válida
    /// antes de ser disponibilizada para processamento.
    /// </summary>
    public void EnsureReadyForQueue()
    {
        if (_recipients.Count == 0)
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir pelo menos um destinatário.");
        }

        if (!_recipients.Any(recipient =>
                recipient.Type == EmailRecipientType.To))
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir pelo menos um destinatário principal do tipo To.");
        }

        if (string.IsNullOrWhiteSpace(Subject))
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir assunto.");
        }

        if (string.IsNullOrWhiteSpace(HtmlBody))
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir corpo HTML.");
        }
    }

    /// <summary>
    /// Registra o início de uma tentativa técnica de envio.
    /// </summary>
    public void RegisterAttempt(
        DateTimeOffset attemptedAt)
    {
        if (Status is EmailMessageStatus.Sent or
            EmailMessageStatus.Canceled)
        {
            throw new InvalidOperationException(
                $"Não é possível registrar nova tentativa para uma mensagem no estado '{Status}'.");
        }

        EnsureReadyForQueue();

        RetryCount++;
        LastAttemptAt = attemptedAt;
    }

    /// <summary>
    /// Marca a mensagem como enviada com sucesso.
    /// </summary>
    public void MarkSent(
        DateTimeOffset sentAt)
    {
        if (Status == EmailMessageStatus.Canceled)
        {
            throw new InvalidOperationException(
                "Uma mensagem cancelada não pode ser marcada como enviada.");
        }

        if (RetryCount <= 0 ||
            LastAttemptAt is null)
        {
            throw new InvalidOperationException(
                "Não é possível marcar a mensagem como enviada antes de registrar uma tentativa.");
        }

        Status = EmailMessageStatus.Sent;
        SentAt = sentAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// Registra falha na última tentativa de envio.
    /// </summary>
    public void MarkFailed(
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (Status is EmailMessageStatus.Sent or
            EmailMessageStatus.Canceled)
        {
            throw new InvalidOperationException(
                $"Não é possível registrar falha para uma mensagem no estado '{Status}'.");
        }

        if (RetryCount <= 0 ||
            LastAttemptAt is null)
        {
            throw new InvalidOperationException(
                "Não é possível registrar falha antes de registrar uma tentativa.");
        }

        Status = EmailMessageStatus.Failed;
        ErrorMessage = errorMessage.Trim();
    }

    /// <summary>
    /// Recoloca uma mensagem falhada na fila para novo processamento manual.
    /// </summary>
    public void Retry()
    {
        if (Status != EmailMessageStatus.Failed)
        {
            throw new InvalidOperationException(
                "Somente mensagens com falha podem ser reenfileiradas manualmente.");
        }

        Status = EmailMessageStatus.Pending;
        RetryCount = 0;
        LastAttemptAt = null;
        ErrorMessage = null;
        SentAt = null;
    }

    /// <summary>
    /// Cancela uma mensagem que ainda não foi enviada.
    /// </summary>
    public void Cancel()
    {
        if (Status == EmailMessageStatus.Sent)
        {
            throw new InvalidOperationException(
                "Uma mensagem já enviada não pode ser cancelada.");
        }

        if (Status == EmailMessageStatus.Canceled)
        {
            return;
        }

        Status = EmailMessageStatus.Canceled;
    }

    private void EnsureCompositionCanBeChanged()
    {
        if (Status != EmailMessageStatus.Pending ||
            RetryCount > 0)
        {
            throw new InvalidOperationException(
                "A composição da mensagem não pode ser alterada após o início do processamento.");
        }
    }

    private static string NormalizeEmail(
        string email)
    {
        return email
            .Trim()
            .ToLowerInvariant();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
