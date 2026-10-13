using FluentValidation.Results;

namespace Atlas.Application.Common.Exceptions;

/// <summary>
/// Exceção lançada quando uma ou mais regras de validação da aplicação falham.
/// </summary>
/// <remarks>
/// Normalmente é utilizada pelo <c>ValidationBehaviour</c> para interromper a execução
/// do handler quando validadores do FluentValidation retornam erros.
/// Os erros são agrupados por propriedade para facilitar a serialização da resposta HTTP.
/// </remarks>
public class ValidationException : Exception
{
    /// <summary>
    /// Inicializa uma nova instância da exceção com a mensagem padrão de falha de validação.
    /// </summary>
    public ValidationException()
        : base("Ocorreram uma ou mais falhas de validação.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Inicializa uma nova instância da exceção a partir das falhas retornadas pelo FluentValidation.
    /// </summary>
    /// <param name="failures">Coleção de falhas de validação retornadas pelos validadores.</param>
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }

    /// <summary>
    /// Inicializa uma nova instância da exceção para uma propriedade específica.
    /// </summary>
    /// <param name="propertyName">Nome da propriedade associada aos erros.</param>
    /// <param name="errors">Mensagens de erro relacionadas à propriedade informada.</param>
    public ValidationException(string propertyName, IEnumerable<string> errors)
        : this(errors.Select(error => new ValidationFailure(propertyName, error)))
    {
    }

    /// <summary>
    /// Inicializa uma nova instância da exceção a partir de um dicionário de erros já agrupados por propriedade.
    /// </summary>
    /// <param name="errors">Dicionário contendo os erros de validação agrupados por propriedade.</param>
    public ValidationException(IDictionary<string, string[]> errors)
        : this()
    {
        Errors = errors;
    }

    /// <summary>
    /// Erros de validação agrupados pelo nome da propriedade.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }
}