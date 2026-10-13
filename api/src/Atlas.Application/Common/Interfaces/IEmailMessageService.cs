using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Define o contrato público para enfileiramento transacional
/// de mensagens de e-mail da Atlas Platform.
/// </summary>
public interface IEmailMessageService
{
    /// <summary>
    /// Constrói e adiciona uma mensagem válida ao Change Tracker.
    ///
    /// O método não executa SaveChangesAsync. A persistência permanece
    /// sob responsabilidade da unidade de trabalho do consumidor.
    /// </summary>
    /// <returns>
    /// A entidade rastreada correspondente à mensagem criada.
    /// O identificador definitivo será atribuído durante SaveChangesAsync.
    /// </returns>
    Task<EmailMessage> EnqueueAsync(
        EmailMessageRequest request,
        CancellationToken cancellationToken = default);
}
