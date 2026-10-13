using Atlas.Application.Business.Indoor.Armazenamento;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Business;

/// <summary>
/// Exceções do produto que não são da plataforma. Registrado antes do
/// <c>ProblemDetailsExceptionHandler</c> (o <c>AddIndoor</c> vem antes do
/// <c>AddWebServices</c>), que segue tratando todo o resto.
/// </summary>
internal sealed class IndoorExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ArmazenamentoIndisponivelException indisponivel)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Storage de mídias indisponível",
            Detail = indisponivel.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        }, cancellationToken);

        return true;
    }
}
