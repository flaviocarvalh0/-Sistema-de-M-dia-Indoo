using Atlas.Application.Platform.EmailMessages.Common;

namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Define o contrato técnico para entrega imediata
/// de uma mensagem de e-mail materializada.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Entrega a mensagem utilizando o mecanismo de transporte configurado.
    /// </summary>
    Task SendEmailAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default);
}
