using System.Globalization;
using System.Text;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Application.Business.Conteudo.Cotacoes;
using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Conteudo.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Atlas.Infrastructure.IntegrationTests.Business.Conteudo;

/// <summary>
/// Os coletores gravando no PostgreSQL real: datas que chegam com fuso de
/// Brasília (-03:00) precisam ir em UTC (o Npgsql recusa outro offset em
/// <c>timestamptz</c>), e as colunas novas precisam aceitar os valores.
/// </summary>
[Collection("DatabaseCollection")]
public class ConteudoExternoIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];

    public Task InitializeAsync() => BancoDeTestes.AtualizarAsync(fixture);

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.FontesNoticia.Where(f => f.Nome.EndsWith(_sufixo)).ExecuteDeleteAsync();
        await context.Cotacoes.Where(c => c.Moeda == "USD" || c.Moeda == "EUR").ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Cotacoes_BoletimDeBrasiliaGravaEmUtc()
    {
        var hoje = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).Date;
        var cliente = new ClienteFixo(url => url.Contains("PTAX", StringComparison.Ordinal) || url.Contains("CotacaoMoedaPeriodo", StringComparison.Ordinal)
            ? $$"""{"value":[{"cotacaoCompra":5.4,"cotacaoVenda":5.41,"dataHoraCotacao":"{{hoje.AddDays(-1):yyyy-MM-dd}} 13:09:27.800","tipoBoletim":"Fechamento PTAX"},{"cotacaoCompra":5.45,"cotacaoVenda":5.4612,"dataHoraCotacao":"{{hoje:yyyy-MM-dd}} 10:04:21.100","tipoBoletim":"Abertura"}]}"""
            : null);

        await using (var context = fixture.CreateDbContext())
        {
            await new ColetorCotacoes(context, cliente, Configuracao(), TimeProvider.System, NullLogger<ColetorCotacoes>.Instance)
                .AtualizarTodasAsync(CancellationToken.None);
        }

        await using (var context = fixture.CreateDbContext())
        {
            var dolar = await context.Cotacoes.SingleAsync(c => c.Moeda == "USD");
            dolar.Venda.Should().Be(5.4612m);
            dolar.FechamentoAnterior.Should().Be(5.41m);
            dolar.CotadaEm.Should().Be(new DateTimeOffset(hoje.Add(new TimeSpan(0, 10, 4, 21, 100)), TimeSpan.FromHours(-3)));
            (await context.Cotacoes.CountAsync(c => c.Moeda == "EUR")).Should().Be(1);
        }
    }

    [Fact]
    public async Task Noticias_DataComFusoDoFeedGravaEmUtc()
    {
        var publicada = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(-3));
        var rss = $"<rss version=\"2.0\"><channel><title>A</title><item><title>Manchete {_sufixo}</title><guid>{_sufixo}</guid>"
            + $"<pubDate>{publicada.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture)} -0300</pubDate></item></channel></rss>";
        var cliente = new ClienteFixo(url => url.EndsWith("/rss", StringComparison.Ordinal) ? rss : null);

        long fonteId;
        await using (var context = fixture.CreateDbContext())
        {
            var fonte = new FonteNoticia { Nome = $"Fonte {_sufixo}", UrlFeed = $"https://{_sufixo}.test/rss", Credito = "Fonte: Teste" };
            context.FontesNoticia.Add(fonte);
            await context.SaveChangesAsync();
            fonteId = fonte.Id;

            var resultado = await new ColetorNoticias(context, Mock.Of<IArmazenamentoMidias>(), cliente, TimeProvider.System, NullLogger<ColetorNoticias>.Instance)
                .ColetarAsync(fonteId, CancellationToken.None);
            resultado.Should().Be(new ResultadoColetaNoticias(1, 0, null));
        }

        await using (var context = fixture.CreateDbContext())
        {
            var noticia = await context.Noticias.SingleAsync(n => n.FonteNoticiaId == fonteId);
            noticia.PublicadaEm.Should().BeCloseTo(publicada, TimeSpan.FromSeconds(1));
        }
    }

    private static IConfiguracaoIndoor Configuracao()
    {
        var configuracao = new Mock<IConfiguracaoIndoor>();
        configuracao.SetupGet(c => c.UrlPtax).Returns(new Uri("https://bcb.test/odata"));
        return configuracao.Object;
    }

    /// <summary>Responde pelo endereço; nulo = "não respondeu".</summary>
    private sealed class ClienteFixo(Func<string, string?> resposta) : IClienteConteudoExterno
    {
        public Task<ConteudoBaixado> BaixarAsync(Uri url, long limiteBytes, CancellationToken cancellationToken, bool truncar = false) =>
            resposta(url.AbsoluteUri) is { } corpo
                ? Task.FromResult(new ConteudoBaixado(Encoding.UTF8.GetBytes(corpo), null))
                : throw new ConteudoExternoIndisponivelException($"{url.Host} não respondeu (teste).");
    }
}
