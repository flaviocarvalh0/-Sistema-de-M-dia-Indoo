namespace Atlas.Application.Platform.EmailMessages.Queries.GetEmailMessages;

public record EmailMessageRecipientDto(
    long Id,
    string? Name,
    string Email,
    EmailRecipientType Type);

public record EmailMessageDto(
    long Id,
    string Subject,
    EmailMessageStatus Status,
    string? ErrorMessage,
    int RetryCount,
    DateTimeOffset? SentAt,
    DateTimeOffset? LastAttemptAt,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTimeOffset Created,
    long? CreatedBy,
    int AttachmentCount,
    int InlineResourceCount,
    bool CanRetry,
    IReadOnlyCollection<EmailMessageRecipientDto> Recipients);

public record GetEmailMessagesQuery
    : IRequest<IEnumerable<EmailMessageDto>>
{
    public string? Recipient { get; init; }

    public EmailMessageStatus? Status { get; init; }

    public string? RelatedEntityType { get; init; }

    public string? RelatedEntityId { get; init; }
}

public class GetEmailMessagesQueryHandler
    : IRequestHandler<
        GetEmailMessagesQuery,
        IEnumerable<EmailMessageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEmailMessagesQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<EmailMessageDto>> Handle(
        GetEmailMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.EmailMessages
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(
                request.Recipient))
        {
            var recipient =
                request.Recipient.Trim();

            query = query.Where(message =>
                message.Recipients.Any(item =>
                    EF.Functions.Like(
                        item.Email,
                        $"%{recipient}%")));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(message =>
                message.Status ==
                request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                request.RelatedEntityType))
        {
            var relatedEntityType =
                request.RelatedEntityType.Trim();

            query = query.Where(message =>
                message.RelatedEntityType ==
                relatedEntityType);
        }

        if (!string.IsNullOrWhiteSpace(
                request.RelatedEntityId))
        {
            var relatedEntityId =
                request.RelatedEntityId.Trim();

            query = query.Where(message =>
                message.RelatedEntityId ==
                relatedEntityId);
        }

        var messages = await query
            .OrderByDescending(message =>
                message.Created)
            .Select(message =>
                new
                {
                    message.Id,
                    message.Subject,
                    message.Status,
                    message.ErrorMessage,
                    message.RetryCount,
                    message.SentAt,
                    message.LastAttemptAt,
                    message.RelatedEntityType,
                    message.RelatedEntityId,
                    message.Created,
                    message.CreatedBy,

                    AttachmentCount =
                        message.Attachments.Count(attachment =>
                            attachment.Disposition ==
                            EmailAttachmentDisposition.Attachment),

                    InlineResourceCount =
                        message.Attachments.Count(attachment =>
                            attachment.Disposition ==
                            EmailAttachmentDisposition.Inline),

                    Recipients =
                        message.Recipients
                            .OrderBy(item =>
                                item.Type)
                            .ThenBy(item =>
                                item.Email)
                            .Select(item =>
                                new EmailMessageRecipientDto(
                                    item.Id,
                                    item.Name,
                                    item.Email,
                                    item.Type))
                            .ToList()
                })
            .ToListAsync(
                cancellationToken);

        return messages.Select(message =>
            new EmailMessageDto(
                message.Id,
                message.Subject,
                message.Status,
                message.ErrorMessage,
                message.RetryCount,
                message.SentAt,
                message.LastAttemptAt,
                message.RelatedEntityType,
                message.RelatedEntityId,
                message.Created,
                message.CreatedBy,
                message.AttachmentCount,
                message.InlineResourceCount,
                message.Status == EmailMessageStatus.Failed,
                message.Recipients));
    }
}
