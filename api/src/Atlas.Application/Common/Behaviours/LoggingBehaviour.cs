using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Common.Behaviours;

/// <summary>
/// PreProcessor do MediatR responsável por registrar as requests recebidas pela aplicação.
/// </summary>
/// <remarks>
/// Este behaviour centraliza o log de entrada das requests para fins de rastreabilidade,
/// diagnóstico e auditoria técnica. O payload é sanitizado antes do registro para evitar
/// vazamento de dados sensíveis em logs estruturados.
/// </remarks>
public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;
    private readonly ILogSanitizer _logSanitizer;

    public LoggingBehaviour(
        ILogger<TRequest> logger,
        IUser user,
        IIdentityService identityService,
        ILogSanitizer logSanitizer)
    {
        _logger = logger;
        _user = user;
        _identityService = identityService;
        _logSanitizer = logSanitizer;
    }

    /// <summary>
    /// Registra informações da request atual antes da execução do respectivo handler.
    /// </summary>
    /// <param name="request">Request sendo processada pelo MediatR.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    public async Task Process(
        TRequest request,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _user.Id ?? 0;
        string? userName = null;

        if (userId > 0)
        {
            userName = await _identityService.GetUserNameAsync(userId);
        }

        // Sanitiza a request antes de registrá-la no log para evitar vazamento de dados sensíveis.
        var safeRequest = _logSanitizer.Sanitize(request);

        _logger.LogInformation(
            "Requisição no Atlas: {RequestName} {UserId} {UserName} {@Request}",
            requestName,
            userId,
            userName,
            safeRequest);
    }
}
