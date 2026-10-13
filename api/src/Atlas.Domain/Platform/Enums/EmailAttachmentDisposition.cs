namespace Atlas.Domain.Platform.Enums;

/// <summary>
/// Define como um arquivo participa da composição MIME
/// de uma mensagem de e-mail.
/// </summary>
public enum EmailAttachmentDisposition
{
    /// <summary>
    /// O arquivo será enviado como anexo convencional.
    /// </summary>
    Attachment = 1,

    /// <summary>
    /// O arquivo será incorporado à mensagem como recurso inline
    /// e poderá ser referenciado pelo HTML através de Content-ID.
    /// </summary>
    Inline = 2
}
