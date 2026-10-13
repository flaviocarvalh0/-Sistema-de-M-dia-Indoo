using System.Reflection;
using Atlas.Application.Business.Dispositivo.Autenticacao;
using Atlas.Application.Business.Dispositivo.Sincronizacao;
using Atlas.Application.Business.Rede.Parceiros.Queries;
using Atlas.Application.Common.Security;
using Atlas.Domain.Business.Constants;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Autorizacao;

/// <summary>
/// Rede de segurança da área de negócio: todo command e query da Mídia Indoor
/// passa pelo <c>AuthorizationBehaviour</c> com uma política conhecida, menos
/// os anônimos listados aqui de propósito. Um command novo sem
/// <c>[Authorize]</c> quebra este teste antes de chegar à produção.
/// </summary>
public class CoberturaAutorizacaoTests
{
    /// <summary>
    /// Sem <c>[Authorize]</c> de usuário, por decisão: o pareamento da TV é
    /// anônimo (limite de tentativas por IP) e a sincronização exige o token
    /// da TV (política <c>Dispositivo</c> no endpoint e <c>IDispositivoAtual</c> no handler).
    /// </summary>
    private static readonly Type[] Anonimos =
    [
        typeof(AutenticarDispositivoCommand),
        typeof(GetSincronizacaoQuery),
    ];

    private static List<Type> Requests() =>
        typeof(GetParceirosQuery).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("Atlas.Application.Business") == true
                && typeof(IBaseRequest).IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();

    public static TheoryData<Type> RequestsDeNegocio() => new(Requests());

    [Theory]
    [MemberData(nameof(RequestsDeNegocio))]
    public void Request_DeNegocio_TemAutorizacaoOuEAnonimoDeclarado(Type request)
    {
        var autorizacoes = request.GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (Anonimos.Contains(request))
        {
            autorizacoes.Should().BeEmpty($"{request.Name} é público por decisão (lista Anonimos)");
            return;
        }

        autorizacoes.Should().NotBeEmpty($"{request.Name} não tem [Authorize] — qualquer usuário logado ou anônimo o executaria");

        foreach (var politica in autorizacoes.Select(a => a.Policy).Where(p => !string.IsNullOrEmpty(p)))
        {
            BusinessPermissionKeys.All().Should().Contain(politica, $"{request.Name} usa uma política que não está registrada");
        }
    }

    [Fact]
    public void Varredura_EncontraOsRequestsDeNegocio()
    {
        // Sem isto, um erro no filtro da varredura faria o teste acima passar
        // por não testar nada.
        var requests = Requests();
        requests.Should().HaveCountGreaterThanOrEqualTo(6);
        Anonimos.Except(requests).Should().BeEmpty("todo anônimo declarado precisa existir na varredura");
    }
}
