namespace Atlas.Domain.Platform.Enums;

/// <summary>
/// Define o papel técnico de um destinatário
/// na composição de uma mensagem de e-mail.
/// </summary>
public enum EmailRecipientType
{
    /// <summary>
    /// Destinatário principal da mensagem.
    /// </summary>
    To = 1,

    /// <summary>
    /// Destinatário em cópia.
    /// </summary>
    Cc = 2,

    /// <summary>
    /// Destinatário em cópia oculta.
    /// </summary>
    Bcc = 3
}
