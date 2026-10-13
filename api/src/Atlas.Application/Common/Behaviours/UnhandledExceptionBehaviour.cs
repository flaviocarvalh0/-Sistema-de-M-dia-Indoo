using Atlas.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogWarning(ex, "Atlas: Conflito de concorrência detectado na requisição {Name} {@Request}", requestName, request);

            throw new ConcurrencyException();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogError(ex, "Requisição no Atlas: Exceção Não Tratada para a Requisição {Name} {@Request}", requestName, request);

            throw;
        }
    }
}
