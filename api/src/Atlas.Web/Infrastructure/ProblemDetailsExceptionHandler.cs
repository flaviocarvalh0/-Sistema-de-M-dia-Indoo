using Atlas.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValidationException = Atlas.Application.Common.Exceptions.ValidationException;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Converte exceções conhecidas da aplicação em respostas <see cref="ProblemDetails"/> em conformidade com a RFC 9110,
/// mapeando <see cref="ValidationException"/> → 400, <see cref="NotFoundException"/> → 404,
/// <see cref="UnauthorizedAccessException"/> → 401, <see cref="ForbiddenAccessException"/> → 403 e
/// <see cref="ConcurrencyException"/> → 409.
/// Exceções não reconhecidas não são tratadas por esta classe e seguem para o middleware padrão.
/// </summary>
public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, problemResult) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, (object)new ValidationProblemDetails(
                new Dictionary<string, string[]>(ve.Errors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ocorreram um ou mais erros de validação.",
                Detail = "Verifique os erros informados e tente novamente.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            }),

            NotFoundException ne => (StatusCodes.Status404NotFound, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Title = "O recurso especificado não foi encontrado.",
                Detail = ne.Message
            }),

            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Detail = "Você precisa estar autenticado para acessar este recurso.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
            }),

            ForbiddenAccessException => (StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acesso proibido",
                Detail = "Você não tem permissão para acessar este recurso.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            }),

            ConcurrencyException ce => (StatusCodes.Status409Conflict, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito de edição",
                Detail = ce.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            }),

            _ => (-1, null)
        };

        if (problemResult is null) return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemResult, cancellationToken);
        return true;
    }
}
