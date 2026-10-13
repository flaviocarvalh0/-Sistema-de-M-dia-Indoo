using System.Security.Claims;
using Atlas.Application.Common.Interfaces;

namespace Atlas.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? Id
    {
        get
        {
            // 1. Extrai o valor textual do claim correspondente ao ID do usuário (sub / NameIdentifier)
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Tenta realizar o parse da string para o formato numérico 'long'
            if (long.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            // 3. Se a requisição for anônima ou o claim for inválido, retorna null de forma segura
            return null;
        }
    }

    public List<string>? Roles =>
        _httpContextAccessor.HttpContext?.User?
            .FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .ToList();
}
