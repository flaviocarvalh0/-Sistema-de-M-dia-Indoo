using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Platform.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Atlas.Infrastructure.Communication.Email;

/// <summary>
/// Implementação SMTP responsável pela entrega imediata
/// de mensagens de e-mail materializadas pela Platform.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IApplicationDbContext context,
        ILogger<SmtpEmailSender> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        var config = await _context.SmtpConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cancellationToken);

        if (config is null)
        {
            _logger.LogError(
                "Falha ao enviar e-mail: nenhuma configuração SMTP cadastrada.");

            throw new InvalidOperationException(
                "O servidor SMTP não está configurado na plataforma.");
        }

        var message = BuildMimeMessage(
            request,
            config.SenderEmail,
            config.SenderName);

        var secureSocketOptions =
            GetSecureSocketOptions(
                config.Port,
                config.EnableSsl);

        using var client =
            new SmtpClient();

        try
        {
            await client.ConnectAsync(
                config.Host,
                config.Port,
                secureSocketOptions,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(
                    config.UserName) &&
                !string.IsNullOrWhiteSpace(
                    config.Password))
            {
                await client.AuthenticateAsync(
                    config.UserName,
                    config.Password,
                    cancellationToken);
            }

            await client.SendAsync(
                message,
                cancellationToken);

            _logger.LogInformation(
                "E-mail entregue ao servidor SMTP {Host} para {RecipientCount} destinatário(s).",
                config.Host,
                request.Recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao enviar e-mail através do host SMTP {Host}.",
                config.Host);

            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(
                    true,
                    cancellationToken);
            }
        }
    }

    private static MimeMessage BuildMimeMessage(
        EmailSendRequest request,
        string senderEmail,
        string? senderName)
    {
        var message =
            new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                senderName ?? senderEmail,
                senderEmail));

        foreach (var recipient in request.Recipients)
        {
            var address =
                new MailboxAddress(
                    recipient.Name ?? recipient.Email,
                    recipient.Email);

            switch (recipient.Type)
            {
                case EmailRecipientType.To:
                    message.To.Add(address);
                    break;

                case EmailRecipientType.Cc:
                    message.Cc.Add(address);
                    break;

                case EmailRecipientType.Bcc:
                    message.Bcc.Add(address);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(recipient.Type),
                        recipient.Type,
                        "O tipo de destinatário informado é inválido.");
            }
        }

        message.Subject =
            request.Subject;

        var bodyBuilder =
            new BodyBuilder
            {
                HtmlBody = request.HtmlBody
            };

        foreach (var attachment in request.Attachments)
        {
            var contentType =
                ContentType.Parse(
                    attachment.ContentType);

            switch (attachment.Disposition)
            {
                case EmailAttachmentDisposition.Attachment:
                    bodyBuilder.Attachments.Add(
                        attachment.FileName,
                        attachment.Content,
                        contentType);
                    break;

                case EmailAttachmentDisposition.Inline:
                    {
                        if (string.IsNullOrWhiteSpace(
                                attachment.ContentId))
                        {
                            throw new InvalidOperationException(
                                "Um recurso inline deve possuir Content-ID.");
                        }

                        var linkedResource =
                            bodyBuilder.LinkedResources.Add(
                                attachment.FileName,
                                attachment.Content,
                                contentType);

                        linkedResource.ContentId =
                            attachment.ContentId;

                        linkedResource.ContentDisposition =
                            new ContentDisposition(
                                ContentDisposition.Inline);

                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(attachment.Disposition),
                        attachment.Disposition,
                        "A disposição do conteúdo informado é inválida.");
            }
        }

        message.Body =
            bodyBuilder.ToMessageBody();

        return message;
    }

    private static void ValidateRequest(
        EmailSendRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.Subject);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.HtmlBody);

        if (request.Recipients.Count == 0)
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir pelo menos um destinatário.");
        }

        if (!request.Recipients.Any(recipient =>
                recipient.Type ==
                    EmailRecipientType.To))
        {
            throw new InvalidOperationException(
                "A mensagem deve possuir pelo menos um destinatário do tipo To.");
        }
    }

    private static SecureSocketOptions GetSecureSocketOptions(
        int port,
        bool enableSsl)
    {
        if (!enableSsl)
        {
            return SecureSocketOptions.None;
        }

        return port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            25 => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.StartTls
        };
    }
}
