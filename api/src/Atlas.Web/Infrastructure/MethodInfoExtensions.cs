using System.Reflection;

namespace Atlas.Web.Infrastructure;

public static class MethodInfoExtensions
{
    // Métodos anônimos gerados pelo compilador (lambdas, funções locais) contêm '<' e '>' em seus nomes.
    private static readonly char[] AnonymousMethodChars = ['<', '>'];

    /// <summary>
    /// Retorna <see langword="true"/> se o método foi gerado pelo compilador a partir de um delegate anônimo ou expressão lambda.
    /// </summary>
    public static bool IsAnonymous(this MethodInfo method) =>
        method.Name.Any(AnonymousMethodChars.Contains);

    /// <summary>
    /// Lança uma <see cref="ArgumentException"/> se o <paramref name="input"/> for um handler anônimo.
    /// Os handlers dos endpoints devem ser métodos nomeados para que um <c>operationId</c> significativo possa ser derivado
    /// a partir do nome do método para uso no OpenAPI e na geração de clientes tipados.
    /// </summary>
    public static void AnonymousMethod(this IGuardClause guardClause, Delegate input)
    {
        if (input.Method.IsAnonymous())
            throw new ArgumentException("O nome do endpoint deve ser especificado ao utilizar handlers anônimos.");
    }
}
