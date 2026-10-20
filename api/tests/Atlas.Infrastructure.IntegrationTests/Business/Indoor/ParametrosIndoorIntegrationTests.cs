using Atlas.Infrastructure.Business;
using Atlas.Infrastructure.Business.Parametros;
using Atlas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Infrastructure.IntegrationTests.Business.Indoor;

/// <summary>Parâmetros da Mídia Indoor em <c>platform.system_parameter</c> (ADR-026).</summary>
[Collection("DatabaseCollection")]
public class ParametrosIndoorIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await BancoDeTestes.AtualizarAsync(fixture);
        await ApagarAsync();
    }

    public Task DisposeAsync() => ApagarAsync();

    [Fact]
    public async Task Subida_DeveCadastrarComOValorDoAppsettingsSemSobrescreverDepois()
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Indoor:Alertas:Emails:0"] = "operadora@rede.test",
            ["Indoor:Alertas:TvOfflineMinutos"] = "7",
        }).Build();
        var parametros = new ParametrosIndoor();
        var sincronizador = Sincronizador(parametros, configuracao);

        await sincronizador.CadastrarFaltantesAsync(CancellationToken.None);

        await using (var context = fixture.CreateDbContext())
        {
            var cadastrados = await context.SystemParameters.Where(p => p.Key.StartsWith("INDOOR.")).ToListAsync();
            cadastrados.Should().HaveCount(ParametrosIndoor.Todos.Count).And.OnlyContain(p => p.Group == ParametrosIndoor.Grupo);

            // Em nome do admin: a tela de Parâmetros só lista linhas com autor.
            var adminId = await context.Users.Where(u => u.NormalizedUserName == "ADMIN").Select(u => (long?)u.Id).FirstOrDefaultAsync();
            cadastrados.Should().OnlyContain(p => p.CreatedBy == adminId && p.LastModifiedBy == adminId);
            cadastrados.Single(p => p.Key == ParametrosIndoor.AlertasEmails.Chave).Value.Should().Be("[\"operadora@rede.test\"]");
            cadastrados.Single(p => p.Key == ParametrosIndoor.AlertasTvOfflineMinutos.Chave).Value.Should().Be("7");
            cadastrados.Single(p => p.Key == ParametrosIndoor.ArmazenamentoLimiteMb.Chave).Value.Should().Be("9216");

            // A operadora muda pelo painel.
            cadastrados.Single(p => p.Key == ParametrosIndoor.AlertasTvOfflineMinutos.Chave).Value = "20";
            await context.SaveChangesAsync();
        }

        await sincronizador.CadastrarFaltantesAsync(CancellationToken.None);
        await sincronizador.LerAsync(CancellationToken.None);

        var lida = new ConfiguracaoIndoor(configuracao, parametros);
        lida.ToleranciaAlertaOffline.Should().Be(TimeSpan.FromMinutes(20), "o valor do painel vale sobre o appsettings e não é sobrescrito");
        lida.EmailsOperadora.Should().Equal("operadora@rede.test");
        parametros.AtualizadoEm.Should().NotBeNull();
    }

    private SincronizadorParametrosIndoor Sincronizador(ParametrosIndoor parametros, IConfiguration configuracao)
    {
        var servicos = new ServiceCollection().AddScoped(_ => fixture.CreateDbContext()).BuildServiceProvider();
        return new SincronizadorParametrosIndoor(
            servicos.GetRequiredService<IServiceScopeFactory>(),
            parametros,
            configuracao,
            TimeProvider.System,
            NullLogger<SincronizadorParametrosIndoor>.Instance);
    }

    private async Task ApagarAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.SystemParameters.Where(p => p.Key.StartsWith("INDOOR.")).ExecuteDeleteAsync();
    }
}
