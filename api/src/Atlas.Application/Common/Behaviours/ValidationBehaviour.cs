namespace Atlas.Application.Common.Behaviours;

/// <summary>
/// Behaviour do MediatR responsável por executar as validações do FluentValidation
/// antes da execução dos handlers da aplicação.
/// </summary>
/// <remarks>
/// Quando houver validadores registrados para o request atual, todos são executados de forma assíncrona.
/// Caso alguma falha seja encontrada, uma <see cref="ValidationException"/> é lançada e o handler não é executado.
/// </remarks>
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
{

    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Executa os validadores associados ao request atual antes de continuar o pipeline.
    /// </summary>
    /// <param name="request">Request sendo processado pelo MediatR.</param>
    /// <param name="next">Delegado responsável por continuar a execução do pipeline.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Resposta produzida pelo próximo handler do pipeline quando não houver falhas de validação.</returns>
    /// <exception cref="ValidationException">
    /// Lançada quando um ou mais validadores retornam falhas para o request atual.
    /// </exception>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
