using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.Platform.Enums;

namespace Atlas.Infrastructure.Communication.Email;

/// <summary>
/// Implementação responsável por construir e adicionar mensagens
/// à fila transacional da Atlas Platform.
/// </summary>
public sealed class EmailMessageService : IEmailMessageService
{
    private readonly IApplicationDbContext _context;

    public EmailMessageService(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmailMessage> EnqueueAsync(
        EmailMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new EmailMessage(
            request.Subject,
            request.HtmlBody,
            request.RelatedEntityType,
            request.RelatedEntityId);

        foreach (var recipient in request.Recipients)
        {
            message.AddRecipient(
                recipient.Email,
                recipient.Type,
                recipient.Name);
        }

        if (request.Attachments.Count > 0)
        {
            await AddAttachmentsAsync(
                message,
                request.Attachments,
                cancellationToken);
        }

        message.EnsureReadyForQueue();

        _context.EmailMessages.Add(message);

        return message;
    }

    private async Task AddAttachmentsAsync(
        EmailMessage message,
        IReadOnlyCollection<EmailAttachmentRequest> attachments,
        CancellationToken cancellationToken)
    {
        var invalidId = attachments
            .FirstOrDefault(item =>
                item.FileContentId <= 0);

        if (invalidId is not null)
        {
            throw new ArgumentException(
                "Todos os anexos devem informar um FileContentId válido.",
                nameof(attachments));
        }

        var fileContentIds = attachments
            .Select(item => item.FileContentId)
            .Distinct()
            .ToArray();

        var fileContents = await _context.FileContents
            .Where(file =>
                fileContentIds.Contains(file.Id))
            .ToDictionaryAsync(
                file => file.Id,
                cancellationToken);

        var missingIds = fileContentIds
            .Where(id =>
                !fileContents.ContainsKey(id))
            .OrderBy(id => id)
            .ToArray();

        if (missingIds.Length > 0)
        {
            throw new KeyNotFoundException(
                $"Os seguintes conteúdos de arquivo não foram encontrados: {string.Join(", ", missingIds)}.");
        }

        foreach (var attachment in attachments)
        {
            var fileContent =
                fileContents[attachment.FileContentId];

            switch (attachment.Disposition)
            {
                case EmailAttachmentDisposition.Attachment:
                    message.AddAttachment(
                        fileContent,
                        attachment.FileName);
                    break;

                case EmailAttachmentDisposition.Inline:
                    message.AddInlineResource(
                        fileContent,
                        attachment.ContentId
                            ?? throw new ArgumentException(
                                "Recursos inline devem informar ContentId.",
                                nameof(attachments)),
                        attachment.FileName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(attachment.Disposition),
                        attachment.Disposition,
                        "A disposição do anexo informada é inválida.");
            }
        }
    }
}
