using Atlas.Application.Common.Interfaces;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Application.Platform.Smtp.Commands.UpsertSmtpConfiguration;
using Atlas.Application.Platform.Smtp.Queries.GetSmtpConfiguration;
using Atlas.Domain.Constants;
using Atlas.Domain.Platform.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Endpoints.Platform;

public class Smtp : IEndpointGroup
{
    public static void Map(
        RouteGroupBuilder groupBuilder)
    {
        groupBuilder
            .WithTags("Servidor de E-mail")
            .WithDescription(
                "Endpoints para consulta, atualização e teste das configurações do servidor SMTP responsável pelo envio de e-mails da plataforma.");

        groupBuilder
            .MapGet(
                "/",
                GetSmtpConfiguration)
            .RequireAuthorization(
                PermissionKeys.Settings.ViewSmtp);

        groupBuilder
            .MapPut(
                "/",
                UpdateSmtpConfiguration)
            .RequireAuthorization(
                PermissionKeys.Settings.ManageSmtp);

        groupBuilder
            .MapPost(
                "test",
                TestSmtpConnection)
            .RequireAuthorization(
                PermissionKeys.Settings.ManageSmtp);
    }

    [EndpointSummary("Obter configuração SMTP")]
    [EndpointDescription(
        "Retorna os parâmetros atuais do servidor de e-mail cadastrado na plataforma (omite a senha por segurança).")]
    public static async Task<Ok<SmtpConfigurationDto?>>
        GetSmtpConfiguration(
            ISender sender,
            CancellationToken cancellationToken)
    {
        var config = await sender.Send(
            new GetSmtpConfigurationQuery(),
            cancellationToken);

        return TypedResults.Ok<SmtpConfigurationDto?>(
            config);
    }

    [EndpointSummary(
        "Salvar ou atualizar configuração SMTP")]
    [EndpointDescription(
        "Grava as credenciais e parâmetros do servidor SMTP. Se já existir uma configuração, atualiza a mesma linha garantindo o registro único.")]
    public static async Task<
        Results<
            NoContent,
            BadRequest<ValidationProblemDetails>>>
        UpdateSmtpConfiguration(
            ISender sender,
            [FromBody]
            UpsertSmtpConfigurationCommand command,
            CancellationToken cancellationToken)
    {
        await sender.Send(
            command,
            cancellationToken);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Testar envio de e-mail")]
    [EndpointDescription(
        "Dispara um e-mail real de teste utilizando o transporte SMTP configurado, sem registrar a mensagem na fila assíncrona.")]
    public static async Task<
        Results<Ok<string>, BadRequest<string>>>
        TestSmtpConnection(
            IEmailSender emailSender,
            [FromQuery] string targetEmail,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                targetEmail))
        {
            return TypedResults.BadRequest(
                "É necessário informar um e-mail de destino válido para o teste.");
        }

        try
        {
            var localTime =
                timeProvider.GetLocalNow();

            var request =
                new EmailSendRequest
                {
                    Subject =
                        "Atlas Framework - Teste de Conexão SMTP",

                    HtmlBody = $"""
                        <h3>Conexão Estabelecida com Sucesso!</h3>
                        <p>Olá,</p>
                        <p>
                            Este é um e-mail automático disparado pelo painel administrativo do
                            <strong>Atlas Framework</strong> para homologar a integração com o servidor SMTP.
                        </p>
                        <br/>
                        <small>
                            Enviado em: {localTime:dd/MM/yyyy HH:mm:ss (zzz)}
                        </small>
                        """,

                    Recipients =
                    [
                        new EmailSendRecipient
                        {
                            Email =
                                targetEmail.Trim(),

                            Type =
                                EmailRecipientType.To
                        }
                    ]
                };

            await emailSender.SendEmailAsync(
                request,
                cancellationToken);

            return TypedResults.Ok(
                "E-mail de teste enviado com sucesso! Verifique sua caixa de entrada.");
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(
                $"Falha na conexão SMTP: {ex.Message}");
        }
    }
}
