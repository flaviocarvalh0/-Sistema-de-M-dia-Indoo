using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Atlas.Application.Common.Behaviours;

/// <summary>
/// Behaviour do MediatR responsável por monitorar o tempo de execução dos requests da aplicação.
/// </summary>
/// <remarks>
/// Quando uma request ultrapassa o limite configurado internamente, registra um alerta com informações
/// de contexto, incluindo nome da request, usuário autenticado e uma versão sanitizada do payload.
/// </remarks>
public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long LongRunningRequestThresholdMilliseconds = 500;

    private readonly ILogger<TRequest> _logger;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;
    private readonly ILogSanitizer _logSanitizer;

    public PerformanceBehaviour(
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
    /// Mede o tempo de execução do request atual e registra um warning quando o tempo exceder
    /// o limite definido para requests longas.
    /// </summary>
    /// <param name="request">Request sendo processado pelo MediatR.</param>
    /// <param name="next">Delegado responsável por continuar a execução do pipeline.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Resposta produzida pelo próximo handler do pipeline.</returns>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        var response = await next();

        timer.Stop();

        var elapsedMilliseconds = timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > LongRunningRequestThresholdMilliseconds)
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

            _logger.LogWarning(
                "Requisição Longa no Atlas: {RequestName} ({ElapsedMilliseconds} milissegundos) {UserId} {UserName} {@Request}",
                requestName,
                elapsedMilliseconds,
                userId,
                userName,
                safeRequest);
        }

        return response;
    }
}