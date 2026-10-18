using System.Security.Claims;
using Atlas.Application.Common.Extensions;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Constants;
using Atlas.Infrastructure.Business;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Autorizacao;

public class BusinessAuthorizationTests
{
    private readonly IAuthorizationService _autorizacao;
    private readonly IAuthorizationPolicyProvider _politicas;

    public BusinessAuthorizationTests()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddBusinessAuthorization()
            .BuildServiceProvider();

        _autorizacao = provider.GetRequiredService<IAuthorizationService>();
        _politicas = provider.GetRequiredService<IAuthorizationPolicyProvider>();
    }

    [Fact]
    public async Task TodaChaveDeNegocio_DeveTerPoliticaRegistrada()
    {
        foreach (var chave in BusinessPermissionKeys.All())
        {
            (await _politicas.GetPolicyAsync(chave)).Should().NotBeNull(because: $"'{chave}' é usada em [Authorize(Policy)]");
        }
    }

    [Fact]
    public async Task Politica_DeveLiberarUsuarioComAClaim()
    {
        var usuario = Autenticado(BusinessPermissionKeys.Telas.View);

        var resultado = await _autorizacao.AuthorizeAsync(usuario, BusinessPermissionKeys.Telas.View);

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Politica_DeveNegarUsuarioSemAClaim()
    {
        // Ter outra permissão da mesma entidade não basta.
        var usuario = Autenticado(BusinessPermissionKeys.Telas.View);

        var resultado = await _autorizacao.AuthorizeAsync(usuario, BusinessPermissionKeys.Telas.Update);

        resultado.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Politica_DeveNegarUsuarioAnonimo()
    {
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(CustomClaimTypes.Permission, BusinessPermissionKeys.Telas.View)]));

        var resultado = await _autorizacao.AuthorizeAsync(anonimo, BusinessPermissionKeys.Telas.View);

        resultado.Succeeded.Should().BeFalse();
    }

    /// <summary>
    /// A chave tem de ser exatamente a claim que o Atlas Studio grava ao
    /// conceder a permissão. Se divergir, nada quebra na compilação — a política
    /// existe, mas nega acesso a todo mundo.
    /// </summary>
    [Theory]
    [InlineData("cliente", BusinessPermissionKeys.Clientes.View, "View")]
    [InlineData("cliente", BusinessPermissionKeys.Clientes.Create, "Create")]
    [InlineData("cliente", BusinessPermissionKeys.Clientes.Update, "Update")]
    [InlineData("cliente", BusinessPermissionKeys.Clientes.Delete, "Delete")]
    [InlineData("tela", BusinessPermissionKeys.Telas.View, "View")]
    [InlineData("tela", BusinessPermissionKeys.Telas.Create, "Create")]
    [InlineData("tela", BusinessPermissionKeys.Telas.Update, "Update")]
    [InlineData("tela", BusinessPermissionKeys.Telas.Delete, "Delete")]
    [InlineData("midia", BusinessPermissionKeys.Midias.View, "View")]
    [InlineData("midia", BusinessPermissionKeys.Midias.Create, "Create")]
    [InlineData("midia", BusinessPermissionKeys.Midias.Update, "Update")]
    [InlineData("midia", BusinessPermissionKeys.Midias.Delete, "Delete")]
    [InlineData("playlist", BusinessPermissionKeys.Playlists.View, "View")]
    [InlineData("playlist", BusinessPermissionKeys.Playlists.Create, "Create")]
    [InlineData("playlist", BusinessPermissionKeys.Playlists.Update, "Update")]
    [InlineData("playlist", BusinessPermissionKeys.Playlists.Delete, "Delete")]
    public void Chave_DeveSerIgualAClaimGravadaPeloStudio(string tabela, string chave, string acao)
    {
        chave.Should().Be($"{tabela.ToPascalCasePlural()}.{acao}");
    }

    private static ClaimsPrincipal Autenticado(params string[] permissoes) =>
        new(new ClaimsIdentity(
            permissoes.Select(p => new Claim(CustomClaimTypes.Permission, p))
                .Append(new Claim(ClaimTypes.NameIdentifier, "10")),
            authenticationType: "Teste"));
}
