using Atlas.Domain.Platform.Enums;
using Atlas.Shared.Observability;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa um destinatário técnico de uma mensagem de e-mail.
/// </summary>
/// <remarks>
/// O destinatário pertence exclusivamente à composição da mensagem
/// e não representa um contato de negócio.
///
/// A Platform não conhece conceitos de destinatários pertencentes
/// às aplicações consumidoras.
/// </remarks>
public class EmailMessageRecipient : BaseAuditableEntity
{
    private EmailMessageRecipient()
    {
    }

    internal EmailMessageRecipient(
        EmailMessage emailMessage,
        string email,
        EmailRecipientType type,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(emailMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!Enum.IsDefined(typeof(EmailRecipientType), type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "O tipo de destinatário informado é inválido.");
        }

        EmailMessage = emailMessage;
        Name = NormalizeOptional(name);
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        Type = type;
    }

    /// <summary>
    /// Identificador da mensagem à qual o destinatário pertence.
    /// </summary>
    public long EmailMessageId { get; private set; }

    /// <summary>
    /// Nome de exibição opcional do destinatário.
    /// </summary>
    [LogSensitive]
    public string? Name { get; private set; }

    /// <summary>
    /// Endereço original informado para o destinatário.
    /// </summary>
    [LogSensitive]
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Versão normalizada do endereço utilizada para
    /// comparação e garantia de unicidade.
    /// </summary>
    [LogSensitive]
    public string NormalizedEmail { get; private set; } = null!;

    /// <summary>
    /// Papel técnico do destinatário na mensagem.
    /// </summary>
    public EmailRecipientType Type { get; private set; }

    /// <summary>
    /// Mensagem à qual o destinatário pertence.
    /// </summary>
    public EmailMessage EmailMessage { get; private set; } = null!;

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
