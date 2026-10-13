using System.Reflection;

namespace Atlas.Web.Infrastructure;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Descobre todas as implementações de <see cref="IEndpointGroup"/> no <paramref name="assembly"/>
    /// e registra cada uma como un grupo de rotas com uma tag correspondente no OpenAPI. O prefixo da rota
    /// tem como padrão <c>/api/{NomeDaClasse}</c>, mas pode ser alterado via <see cref="IEndpointGroup.RoutePrefix"/>.
    /// </summary>
    public static WebApplication MapEndpoints(this WebApplication app, Assembly assembly)
    {
        var endpointGroupTypes = assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.IsAssignableTo(typeof(IEndpointGroup)));

        foreach (var type in endpointGroupTypes)
        {
            var groupName = type.Name;
            var routePrefix = type.GetProperty(nameof(IEndpointGroup.RoutePrefix))
                ?.GetValue(null) as string ?? $"/api/{groupName}";

            var group = app.MapGroup(routePrefix)
                .WithTags(groupName);

            type.GetMethod(nameof(IEndpointGroup.Map))!.Invoke(null, [group]);
        }

        return app;
    }
}
