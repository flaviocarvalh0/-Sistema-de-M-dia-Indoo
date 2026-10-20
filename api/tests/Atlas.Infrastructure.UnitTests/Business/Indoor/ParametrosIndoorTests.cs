using Atlas.Infrastructure.Business;
using Atlas.Infrastructure.Business.Parametros;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Parâmetro cadastrado → appsettings → padrão (ADR-026).</summary>
public class ParametrosIndoorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static IConfiguration Configuracao(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Chave, v.Valor)))
            .Build();

    private static ParametrosIndoor Cadastrados(params (DefinicaoParametro Definicao, string Valor)[] valores)
    {
        var parametros = new ParametrosIndoor();
        parametros.Atualizar(valores.Select(v => new KeyValuePair<string, string>(v.Definicao.Chave, v.Valor)), Agora);
        return parametros;
    }

    [Fact]
    public void ParametroCadastrado_DeveValerSobreOAppsettings()
    {
        var configuracao = new ConfiguracaoIndoor(
            Configuracao(("Indoor:Alertas:TvOfflineMinutos", "30"), ("Indoor:Coleta:Ativa", "true")),
            Cadastrados((ParametrosIndoor.AlertasTvOfflineMinutos, "8"), (ParametrosIndoor.ColetaAtiva, "false")));

        configuracao.ToleranciaAlertaOffline.Should().Be(TimeSpan.FromMinutes(8));
        configuracao.ColetaAutomatica.Should().BeFalse();
    }

    [Fact]
    public void SemParametro_DeveUsarOAppsettingsEDepoisOPadrao()
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(("Indoor:Alertas:HoraInicio", "6")), Cadastrados());

        configuracao.HorarioAlertas.Should().Be((6, 22));
        configuracao.IntervaloSincronizacao.Should().Be(TimeSpan.FromSeconds(60));
        configuracao.ColetaAutomatica.Should().BeTrue();
    }

    [Fact]
    public void Minimos_ETetoDe10Gb_ValemTambemParaOParametro()
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(), Cadastrados(
            (ParametrosIndoor.AlertasTvOfflineMinutos, "1"),
            (ParametrosIndoor.ArmazenamentoLimiteMb, "50000"),
            (ParametrosIndoor.DispositivoIntervaloSegundos, "10.0")));

        configuracao.ToleranciaAlertaOffline.Should().Be(TimeSpan.FromMinutes(5));
        configuracao.LimiteArmazenamentoBytes.Should().Be(10240L * 1024 * 1024);
        configuracao.IntervaloSincronizacao.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData("[\"a@rede.test\", \"b@rede.test\"]")]
    [InlineData("a@rede.test; b@rede.test")]
    [InlineData("a@rede.test,b@rede.test,sem-arroba")]
    public void Emails_AceitaJsonOuSeparadosPorVirgula(string valor)
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(("Indoor:Alertas:Emails:0", "velho@rede.test")),
            Cadastrados((ParametrosIndoor.AlertasEmails, valor)));

        configuracao.EmailsOperadora.Should().Equal("a@rede.test", "b@rede.test");
    }

    [Fact]
    public void ListaVaziaCadastrada_DesligaOsEmailsMesmoComAppsettings()
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(("Indoor:Alertas:Emails:0", "velho@rede.test")),
            Cadastrados((ParametrosIndoor.AlertasEmails, "[]")));

        configuracao.EmailsOperadora.Should().BeEmpty();
    }

    [Fact]
    public void ValorInvalido_CaiNoAppsettings()
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(("Indoor:Coleta:NoticiasMinutos", "45")),
            Cadastrados((ParametrosIndoor.ColetaNoticiasMinutos, "abc")));

        configuracao.IntervaloColetaNoticias.Should().Be(TimeSpan.FromMinutes(45));
    }

    [Theory]
    [InlineData("https://localhost:7216")]
    [InlineData("http://127.0.0.1:5366")]
    public void UrlPublicaLocal_ViraNula_OCelularNaoChegaria(string url)
    {
        var configuracao = new ConfiguracaoIndoor(Configuracao(("Indoor:UrlPublica", url)), Cadastrados());

        configuracao.UrlPublica.Should().BeNull();
    }

    [Fact]
    public void UrlPublicaEContato_DoPainelValemSobreOAppsettings()
    {
        var configuracao = new ConfiguracaoIndoor(
            Configuracao(("Indoor:UrlPublica", "https://antiga.test")),
            Cadastrados((ParametrosIndoor.UrlPublica, "https://tv.rede.test"), (ParametrosIndoor.AnuncieWhatsApp, "(92) 99999-0000")));

        configuracao.UrlPublica.Should().Be(new Uri("https://tv.rede.test"));
        configuracao.WhatsAppAnuncie.Should().Be("5592999990000");
        new ConfiguracaoIndoor(Configuracao(), Cadastrados((ParametrosIndoor.AnuncieWhatsApp, "123"))).WhatsAppAnuncie.Should().BeNull("telefone inválido = sem convite");
    }
}
