namespace Atlas.Web.Infrastructure;

/// <summary>
/// Define um grupo de endpoints relacionados do Minimal APIs.
/// As implementações são descobertas e registradas automaticamente como um grupo de rotas com uma tag
/// correspondente no OpenAPI. Por padrão, o prefixo da rota é <c>/api/{NomeDaClasse}</c>; altere
/// <see cref="RoutePrefix"/> para usar um caminho customizado, incluindo caminhos de recursos aninhados, como
/// <c>/api/TodoLists/{todoListId}/TodoItems</c>.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>
    /// O prefixo de rota para este grupo de endpoints.
    /// O padrão é <c>/api/{NomeDaClasse}</c>. Substitua para especificar um caminho customizado ou aninhado.
    /// </summary>
    static virtual string? RoutePrefix => null;

    /// <summary>
    /// Mapeia as rotas e endpoints pertencentes a este grupo.
    /// </summary>
    /// <param name="groupBuilder">O construtor de grupo de rotas utilizado para configurar os endpoints.</param>
    static abstract void Map(RouteGroupBuilder groupBuilder);
}
