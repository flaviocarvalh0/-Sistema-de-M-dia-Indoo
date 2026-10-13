using System.Diagnostics.CodeAnalysis;

namespace Atlas.Web.Infrastructure;

/// <summary>
/// Estende <see cref="IEndpointRouteBuilder"/> com sobrecargas de conveniência utilizadas dentro de
/// <see cref="IEndpointGroup.Map"/>. Cada método encapsula a chamada padrão <c>Map{Verbo}</c> do ASP.NET Core
/// e deriva automaticamente o nome do endpoint a partir do nome do método do handler. Esse nome se torna o
/// <c>operationId</c> no OpenAPI e é utilizado para a geração de clientes tipados (ex: <c>nswag</c>).
/// <para>
/// O parâmetro <c>pattern</c> (padrão de rota) é opcional para GET e POST (operações em nível de coleção que normalmente
/// não possuem parâmetros na rota), mas é obrigatório para PUT, PATCH e DELETE (operações em nível de recurso
/// que quase sempre visam um item específico por ID, ex: <c>"{id}"</c>).
/// </para>
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <inheritdoc cref="EndpointRouteBuilderExtensions"/>
    public static RouteHandlerBuilder MapGet(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern = "")
    {
        Guard.Against.AnonymousMethod(handler);

        return builder.MapGet(pattern, handler)
                      .WithName(handler.Method.Name);
    }

    /// <inheritdoc cref="EndpointRouteBuilderExtensions"/>
    public static RouteHandlerBuilder MapPost(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern = "")
    {
        Guard.Against.AnonymousMethod(handler);

        return builder.MapPost(pattern, handler)
                      .WithName(handler.Method.Name);
    }

    /// <inheritdoc cref="EndpointRouteBuilderExtensions"/>
    public static RouteHandlerBuilder MapPut(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern)
    {
        Guard.Against.AnonymousMethod(handler);

        return builder.MapPut(pattern, handler)
                      .WithName(handler.Method.Name);
    }

    /// <inheritdoc cref="EndpointRouteBuilderExtensions"/>
    public static RouteHandlerBuilder MapPatch(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern)
    {
        Guard.Against.AnonymousMethod(handler);

        return builder.MapPatch(pattern, handler)
                      .WithName(handler.Method.Name);
    }

    /// <inheritdoc cref="EndpointRouteBuilderExtensions"/>
    public static RouteHandlerBuilder MapDelete(this IEndpointRouteBuilder builder, Delegate handler, [StringSyntax("Route")] string pattern)
    {
        Guard.Against.AnonymousMethod(handler);

        return builder.MapDelete(pattern, handler)
                      .WithName(handler.Method.Name);
    }
}
