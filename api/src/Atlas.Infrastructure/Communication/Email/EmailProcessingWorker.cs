using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Platform.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Communication.Email;

/// <summary>
/// Worker responsável por processar a fila técnica de e-mails
/// da Atlas Platform.
/// </summary>
public sealed class EmailProcessingWorker
    : BackgroundService
{
    private const int MaxRetryAttempts = 3;
    private const int BatchSize = 10;

    /// <summary>
    /// Chave exclusiva utilizada pelo PostgreSQL Advisory Lock
    /// para garantir um único processador da fila de e-mails
    /// por banco de dados.
    /// </summary>
    private const long EmailProcessingAdvisoryLockId =
        2026090201;

    private static readonly TimeSpan ExecutionInterval =
        TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailProcessingWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public EmailProcessingWorker(
        IServiceProvider serviceProvider,
        ILogger<EmailProcessingWorker> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "O motor de processamento de e-mails em segundo plano foi iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmailsAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ocorreu uma falha crítica no ciclo do Worker de e-mails.");
            }

            try
            {
                await Task.Delay(
                    ExecutionInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingEmailsAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _serviceProvider.CreateScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<IApplicationDbContext>();

        var emailSender =
            scope.ServiceProvider
                .GetRequiredService<IEmailSender>();

        var connection =
            context.Database.GetDbConnection();

        var connectionWasClosed =
            connection.State == ConnectionState.Closed;

        if (connectionWasClosed)
        {
            await context.Database.OpenConnectionAsync(
                cancellationToken);
        }

        var lockAcquired = false;

        try
        {
            await using (var lockCommand =
                connection.CreateCommand())
            {
                lockCommand.CommandText =
                    "SELECT pg_try_advisory_lock(@lock_id);";

                var parameter =
                    lockCommand.CreateParameter();

                parameter.ParameterName =
                    "lock_id";

                parameter.Value =
                    EmailProcessingAdvisoryLockId;

                lockCommand.Parameters.Add(
                    parameter);

                var result =
                    await lockCommand.ExecuteScalarAsync(
                        cancellationToken);

                lockAcquired =
                    result is bool acquired &&
                    acquired;
            }

            if (!lockAcquired)
            {
                _logger.LogDebug(
                    "Outro processador já possui o lock da fila técnica de e-mails.");

                return;
            }

            var messagesToProcess =
                await context.EmailMessages
                    .Include(message =>
                        message.Recipients)
                    .Include(message =>
                        message.Attachments)
                        .ThenInclude(attachment =>
                            attachment.FileContent)
                    .Where(message =>
                        message.Status ==
                            EmailMessageStatus.Pending ||
                        (
                            message.Status ==
                                EmailMessageStatus.Failed &&
                            message.RetryCount <
                                MaxRetryAttempts
                        ))
                    .OrderBy(message =>
                        message.Created)
                    .Take(BatchSize)
                    .ToListAsync(
                        cancellationToken);

            if (messagesToProcess.Count == 0)
            {
                return;
            }

            foreach (var message in messagesToProcess)
            {
                try
                {
                    message.RegisterAttempt(
                        _timeProvider.GetUtcNow());

                    var sendRequest =
                        BuildSendRequest(message);

                    await emailSender.SendEmailAsync(
                        sendRequest,
                        cancellationToken);

                    message.MarkSent(
                        _timeProvider.GetUtcNow());

                    _logger.LogInformation(
                        "Mensagem de e-mail ID {EmailMessageId} enviada com sucesso para {RecipientCount} destinatário(s).",
                        message.Id,
                        message.Recipients.Count);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    try
                    {
                        message.MarkFailed(
                            ex.Message);
                    }
                    catch (Exception stateException)
                    {
                        _logger.LogError(
                            stateException,
                            "Não foi possível registrar a falha da mensagem de e-mail ID {EmailMessageId}.",
                            message.Id);
                    }

                    _logger.LogWarning(
                        ex,
                        "Falha ao processar a mensagem de e-mail ID {EmailMessageId}.",
                        message.Id);
                }

                await context.SaveChangesAsync(
                    cancellationToken);
            }
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    await using var unlockCommand =
                        connection.CreateCommand();

                    unlockCommand.CommandText =
                        "SELECT pg_advisory_unlock(@lock_id);";

                    var parameter =
                        unlockCommand.CreateParameter();

                    parameter.ParameterName =
                        "lock_id";

                    parameter.Value =
                        EmailProcessingAdvisoryLockId;

                    unlockCommand.Parameters.Add(
                        parameter);

                    await unlockCommand.ExecuteScalarAsync(
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Não foi possível liberar explicitamente o advisory lock da fila de e-mails. O PostgreSQL o liberará quando a conexão for encerrada.");
                }
            }

            if (connectionWasClosed)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private static EmailSendRequest BuildSendRequest(
        Atlas.Domain.Platform.Entities.EmailMessage message)
    {
        return new EmailSendRequest
        {
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,

            Recipients = message.Recipients
                .Select(recipient =>
                    new EmailSendRecipient
                    {
                        Name = recipient.Name,
                        Email = recipient.Email,
                        Type = recipient.Type
                    })
                .ToArray(),

            Attachments = message.Attachments
                .Select(attachment =>
                    new EmailSendAttachment
                    {
                        FileName =
                            attachment.FileName
                            ?? attachment.FileContent.FileName,

                        ContentType =
                            attachment.FileContent.ContentType,

                        Content =
                            attachment.FileContent.Content,

                        Disposition =
                            attachment.Disposition,

                        ContentId =
                            attachment.ContentId
                    })
                .ToArray()
        };
    }
}
