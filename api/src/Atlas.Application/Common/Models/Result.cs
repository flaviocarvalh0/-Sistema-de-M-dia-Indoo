namespace Atlas.Application.Common.Models;

/// <summary>
/// Representa o resultado de uma operação da aplicação que pode ser concluída com sucesso ou falha.
/// </summary>
/// <remarks>
/// Este tipo é utilizado para padronizar retornos de casos de uso, evitando o uso de exceções
/// para fluxos esperados de negócio, validação ou autenticação.
/// </remarks>
public class Result
{
    internal Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors?.ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Indica se a operação foi concluída com sucesso.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Lista de mensagens de erro quando a operação falhar.
    /// </summary>
    public string[] Errors { get; init; }

    /// <summary>
    /// Mensagem consolidada contendo todos os erros separados por ponto e vírgula.
    /// </summary>
    public string ErrorMessage => string.Join("; ", Errors);

    /// <summary>
    /// Cria um resultado indicando sucesso.
    /// </summary>
    public static Result Success()
    {
        return new Result(true, Array.Empty<string>());
    }

    /// <summary>
    /// Cria um resultado indicando falha com as mensagens de erro informadas.
    /// </summary>
    /// <param name="errors">Mensagens que descrevem os motivos da falha.</param>
    public static Result Failure(IEnumerable<string> errors)
    {
        return new Result(false, errors);
    }
}

/// <summary>
/// Representa o resultado de uma operação da aplicação que, quando bem-sucedida,
/// retorna um valor do tipo especificado.
/// </summary>
/// <typeparam name="T">Tipo do valor retornado pela operação em caso de sucesso.</typeparam>
public class Result<T> : Result
{
    internal Result(bool succeeded, T? value, IEnumerable<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    /// <summary>
    /// Valor retornado pela operação quando concluída com sucesso.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Cria um resultado de sucesso contendo o valor informado.
    /// </summary>
    /// <param name="value">Valor retornado pela operação.</param>
    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, Array.Empty<string>());
    }

    /// <summary>
    /// Cria um resultado de falha com as mensagens de erro informadas.
    /// </summary>
    /// <param name="errors">Mensagens que descrevem os motivos da falha.</param>
    public new static Result<T> Failure(IEnumerable<string> errors)
    {
        return new Result<T>(false, default, errors);
    }
}