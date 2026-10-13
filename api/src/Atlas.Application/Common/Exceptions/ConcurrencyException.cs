namespace Atlas.Application.Common.Exceptions;
public class ConcurrencyException : Exception
{
    private const string DefaultMessage =
        "Não foi possível concluir a operação porque o registro foi modificado por outra operação concorrente.";

    public ConcurrencyException()
        : base(DefaultMessage)
    {
    }

    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
