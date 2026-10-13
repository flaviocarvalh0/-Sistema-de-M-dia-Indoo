using Atlas.Application.Platform.EmailMessages.Commands.RetryEmailMessage;
using Atlas.Application.Platform.EmailMessages.Queries.GetEmailMessages;
using Atlas.Domain.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Web.Endpoints.Platform
{
    public class EmailMessages : IEndpointGroup
    {
        public static void Map(RouteGroupBuilder groupBuilder)
        {
            groupBuilder
                .WithTags("Mensagens de E-mail")
                .WithDescription("Endpoints para monitoramento, auditoria e reprocessamento do fluxo assíncrono de e-mails.");

            // Rota para listar
            groupBuilder
                .MapGet("/", GetEmails)
                .RequireAuthorization(PermissionKeys.EmailMessages.View);

            // Rota para disparar o reenvio manual
            groupBuilder
                .MapPost("/{id:long}/retry", RetryEmail)
                .RequireAuthorization(PermissionKeys.EmailMessages.Retry);
        }

        [EndpointSummary("Consultar histórico de e-mails")]
        public static async Task<Ok<IEnumerable<EmailMessageDto>>> GetEmails(
            ISender sender,
            [AsParameters] GetEmailMessagesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);
            return TypedResults.Ok(result);
        }

        [EndpointSummary("Forçar reenvio manual de e-mail falhado")]
        public static async Task<Results<NoContent, BadRequest<string>>> RetryEmail(
            ISender sender,
            long id,
            CancellationToken cancellationToken)
        {
            try
            {
                await sender.Send(new RetryEmailMessageCommand(id), cancellationToken);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        }
    }
}
