using System.Reflection;
using Atlas.Application.Common.Exceptions;

namespace Atlas.Application.Common.Behaviours;

/// <summary>
/// Behaviour do MediatR responsável por validar as regras de autorização declaradas nos requests da aplicação.
/// </summary>
/// <remarks>
/// Antes da execução do handler, verifica os atributos <see cref="AuthorizeAttribute"/> aplicados ao request.
/// Quando houver roles ou policies configuradas, valida se o usuário autenticado possui acesso suficiente
/// para continuar o fluxo.
/// </remarks>
public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public AuthorizationBehaviour(
        IUser user,
        IIdentityService identityService)
    {
        _user = user;
        _identityService = identityService;
    }

    /// <summary>
    /// Executa a validação de autorização do request atual antes de permitir a chamada do próximo handler no pipeline.
    /// </summary>
    /// <param name="request">Request sendo processado pelo MediatR.</param>
    /// <param name="next">Delegado responsável por continuar a execução do pipeline.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação assíncrona.</param>
    /// <returns>Resposta produzida pelo próximo handler do pipeline quando a autorização for bem-sucedida.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Lançada quando o request exige autorização, mas não há usuário autenticado.
    /// </exception>
    /// <exception cref="ForbiddenAccessException">
    /// Lançada quando o usuário autenticado não possui as roles ou policies exigidas pelo request.
    /// </exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizeAttributes = request
            .GetType()
            .GetCustomAttributes<AuthorizeAttribute>();

        if (authorizeAttributes.Any())
        {
            if (_user.Id == null || _user.Id <= 0)
            {
                throw new UnauthorizedAccessException();
            }

            if (await _identityService.IsSuperUserAsync(_user.Id.Value))
            {
                return await next();
            }

            var authorizeAttributesWithRoles = authorizeAttributes
                .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
                .ToArray();

            if (authorizeAttributesWithRoles.Any())
            {
                var authorized = false;

                foreach (var roles in authorizeAttributesWithRoles.Select(a => a.Roles.Split(',')))
                {
                    foreach (var role in roles.Select(r => r.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)))
                    {
                        var isInRole = _user.Roles?.Any(userRole =>
                            string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase)) ?? false;

                        if (isInRole)
                        {
                            authorized = true;
                            break;
                        }
                    }

                    if (authorized)
                    {
                        break;
                    }
                }

                if (!authorized)
                {
                    throw new ForbiddenAccessException();
                }
            }

            var authorizeAttributesWithPolicies = authorizeAttributes
                .Where(a => !string.IsNullOrWhiteSpace(a.Policy))
                .ToArray();

            if (authorizeAttributesWithPolicies.Any())
            {
                if (await _identityService.IsSuperUserAsync(_user.Id.Value))
                {
                    return await next();
                }

                foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
                {
                    var authorized = await _identityService.AuthorizeAsync(_user.Id.Value, policy);

                    if (!authorized)
                    {
                        throw new ForbiddenAccessException();
                    }
                }
            }
        }

        return await next();
    }
}



