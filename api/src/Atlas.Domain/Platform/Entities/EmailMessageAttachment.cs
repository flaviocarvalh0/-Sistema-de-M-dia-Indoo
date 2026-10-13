using Atlas.Domain.Platform.Enums;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// Representa a participação técnica de um conteúdo binário
/// na composição MIME de uma mensagem de e-mail.
/// </summary>
/// <remarks>
/// O conteúdo propriamente dito permanece armazenado em
/// <see cref="FileContent"/>.
///
/// Esta entidade define apenas como aquele conteúdo participa
/// da mensagem: como anexo convencional ou recurso inline.
/// </remarks>
public class EmailMessageAttachment : BaseAuditableEntity
{
    private EmailMessageAttachment()
    {
    }

    internal EmailMessageAttachment(
        EmailMessage emailMessage,
        FileContent fileContent,
        EmailAttachmentDisposition disposition,
        string? fileName = null,
        string? contentId = null)
    {
        ArgumentNullException.ThrowIfNull(emailMessage);
        ArgumentNullException.ThrowIfNull(fileContent);

        if (!Enum.IsDefined(
                typeof(EmailAttachmentDisposition),
                disposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(disposition),
                "A disposição do anexo informada é inválida.");
        }

        var normalizedFileName =
            NormalizeOptional(fileName);

        var normalizedContentId =
            NormalizeOptional(contentId);

        if (disposition == EmailAttachmentDisposition.Attachment &&
            normalizedContentId is not null)
        {
            throw new ArgumentException(
                "Um anexo convencional não pode possuir Content-ID.",
                nameof(contentId));
        }

        if (disposition == EmailAttachmentDisposition.Inline &&
            normalizedContentId is null)
        {
            throw new ArgumentException(
                "Um recurso inline deve possuir Content-ID.",
                nameof(contentId));
        }

        EmailMessage = emailMessage;
        FileContent = fileContent;
        Disposition = disposition;
        FileName = normalizedFileName;
        ContentId = normalizedContentId;
    }

    /// <summary>
    /// Identificador da mensagem à qual o arquivo pertence.
    /// </summary>
    public long EmailMessageId { get; private set; }

    /// <summary>
    /// Identificador do conteúdo binário reutilizado pela mensagem.
    /// </summary>
    public long FileContentId { get; private set; }

    /// <summary>
    /// Define se o conteúdo será um attachment convencional
    /// ou um recurso inline.
    /// </summary>
    public EmailAttachmentDisposition Disposition { get; private set; }

    /// <summary>
    /// Nome opcional utilizado na apresentação do arquivo no e-mail.
    ///
    /// Quando nulo, deverá ser utilizado
    /// <see cref="FileContent.FileName"/>.
    /// </summary>
    public string? FileName { get; private set; }

    /// <summary>
    /// Content-ID utilizado para recursos inline.
    ///
    /// Obrigatório quando <see cref="Disposition"/> for
    /// <see cref="EmailAttachmentDisposition.Inline"/>.
    /// </summary>
    public string? ContentId { get; private set; }

    /// <summary>
    /// Mensagem à qual o arquivo pertence.
    /// </summary>
    public EmailMessage EmailMessage { get; private set; } = null!;

    /// <summary>
    /// Conteúdo binário reutilizado pela mensagem.
    /// </summary>
    public FileContent FileContent { get; private set; } = null!;

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
