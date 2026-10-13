using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Dispositivo.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Infrastructure.Business.Dispositivo;

/// <summary>
/// Autenticação das TVs: esquema JWT próprio (<see cref="Esquema"/>), com a
/// mesma chave de assinatura da plataforma mas audiência própria. O JwtBearer
/// da plataforma recusa este token (audiência diferente e sem sessão de
/// usuário) e este esquema recusa o token de usuário: um não abre a porta do outro.
/// </summary>
public static class AutenticacaoDispositivo
{
    public const string Esquema = "Dispositivo";

    /// <summary>Política exigida nas rotas da TV (autentica pelo <see cref="Esquema"/>).</summary>
    public const string Politica = "Dispositivo";

    public const string Audiencia = "Indoor.Dispositivos";

    public static void AddAutenticacaoDispositivo(this IServiceCollection services, IConfiguration configuration)
    {
        var (emissor, chave) = LerChave(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICredencialDispositivo, CredencialDispositivoHttp>();
        services.AddScoped<IDispositivoAtual, DispositivoAtual>();
        services.AddSingleton<IGeradorTokenDispositivo, GeradorTokenDispositivo>();

        services
            .AddAuthentication()
            .AddJwtBearer(Esquema, options =>
            {
                // Os nomes das claims ficam como emitidos (sem o mapeamento para os URIs do .NET).
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = emissor,
                    ValidateAudience = true,
                    ValidAudience = Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = chave,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politica, policy =>
            {
                policy.AddAuthenticationSchemes(Esquema);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimsDispositivo.TelaId);
            });
    }

    internal static (string Emissor, SymmetricSecurityKey Chave) LerChave(IConfiguration configuration)
    {
        var emissor = configuration["Jwt:Issuer"];
        var chave = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(emissor) || string.IsNullOrWhiteSpace(chave))
        {
            throw new InvalidOperationException("Jwt:Issuer e Jwt:Key são obrigatórios para o token das TVs.");
        }

        return (emissor, new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)));
    }
}

/// <summary>Emite o JWT da TV (HMAC-SHA256 com a chave da plataforma).</summary>
internal sealed class GeradorTokenDispositivo(IConfiguration configuration, IConfiguracaoIndoor configuracao, TimeProvider timeProvider)
    : IGeradorTokenDispositivo
{
    private readonly (string Emissor, SymmetricSecurityKey Chave) _assinatura = AutenticacaoDispositivo.LerChave(configuration);

    public TokenDispositivo Gerar(long telaId, long parceiroId, string dispositivoId, int versaoCredencial)
    {
        var agora = timeProvider.GetUtcNow();
        var expiraEm = agora + configuracao.ValidadeTokenDispositivo;

        var token = new JwtSecurityToken(
            issuer: _assinatura.Emissor,
            audience: AutenticacaoDispositivo.Audiencia,
            claims:
            [
                new Claim(ClaimsDispositivo.TelaId, telaId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimsDispositivo.ParceiroId, parceiroId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimsDispositivo.DispositivoId, dispositivoId),
                new Claim(ClaimsDispositivo.VersaoCredencial, versaoCredencial.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ],
            notBefore: agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: new SigningCredentials(_assinatura.Chave, SecurityAlgorithms.HmacSha256));

        // O JWT guarda a validade em segundos: devolve o mesmo instante que vai no token.
        return new TokenDispositivo(new JwtSecurityTokenHandler().WriteToken(token), DateTimeOffset.FromUnixTimeSeconds(expiraEm.ToUnixTimeSeconds()));
    }
}

/// <summary>Lê as claims do token da TV na requisição atual.</summary>
internal sealed class CredencialDispositivoHttp(IHttpContextAccessor httpContextAccessor) : ICredencialDispositivo
{
    private ClaimsPrincipal? Usuario => httpContextAccessor.HttpContext?.User;

    public long? TelaId => Longo(ClaimsDispositivo.TelaId);
    public long? ParceiroId => Longo(ClaimsDispositivo.ParceiroId);
    public string? DispositivoId => Usuario?.FindFirst(ClaimsDispositivo.DispositivoId)?.Value;
    public int? VersaoCredencial => int.TryParse(Usuario?.FindFirst(ClaimsDispositivo.VersaoCredencial)?.Value, out var v) ? v : null;

    public DateTimeOffset? ExpiraEm =>
        long.TryParse(Usuario?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos)
            : null;

    private long? Longo(string tipo) => long.TryParse(Usuario?.FindFirst(tipo)?.Value, out var v) ? v : null;
}
