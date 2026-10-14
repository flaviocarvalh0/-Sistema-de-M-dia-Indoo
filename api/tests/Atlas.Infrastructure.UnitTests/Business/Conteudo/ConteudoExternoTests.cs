using System.Text;
using Atlas.Application.Business.Conteudo.Clima;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Application.Business.Conteudo.Noticias;
using Atlas.Application.Business.Rede.FontesNoticia;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.UnitTests.Business.Indoor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ValidationException = Atlas.Application.Common.Exceptions.ValidationException;

namespace Atlas.Infrastructure.UnitTests.Business.Conteudo;

/// <summary>Leitura de feeds, coleta de notícias, clima e o cadastro das fontes.</summary>
public class ConteudoExternoTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF89a-imagem");

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly ClienteFake _cliente = new();
    private readonly RelogioFixo _relogio = new(Agora);

    public void Dispose() => _context.Dispose();

    // ---------- Leitor de feed ----------

    private const string Rss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/" xmlns:content="http://purl.org/rss/1.0/modules/content/">
          <channel>
            <title>Agência</title>
            <item>
              <title>Governo anuncia &amp; detalha &lt;b&gt;plano&lt;/b&gt;</title>
              <link>https://a.test/noticia/1</link>
              <guid isPermaLink="false">id-1</guid>
              <pubDate>Wed, 14 Oct 2026 11:30:00 -0300</pubDate>
              <enclosure url="https://a.test/img/1.jpg" type="image/jpeg" length="10"/>
            </item>
            <item>
              <title>Com media:content</title>
              <link>https://a.test/noticia/2</link>
              <pubDate>Wed, 14 Oct 2026 13:00:00 GMT</pubDate>
              <media:content url="https://a.test/img/2.png" medium="image"/>
            </item>
            <item>
              <title>Imagem no texto</title>
              <guid>id-3</guid>
              <pubDate>14 Oct 2026 10:00 BRT</pubDate>
              <description><![CDATA[<p><img alt="x" src="/img/3.jpg?w=800" /> Texto</p>]]></description>
            </item>
            <item>
              <title>Sem imagem nem data</title>
              <guid>id-4</guid>
            </item>
            <item><link>https://a.test/sem-titulo</link></item>
          </channel>
        </rss>
        """;

    [Fact]
    public void LeitorFeed_LeRssComAsTresFormasDeImagem()
    {
        var itens = LeitorFeed.Ler(Encoding.UTF8.GetBytes(Rss), new Uri("https://a.test/rss"));

        itens.Should().HaveCount(4, "item sem título fica de fora");

        itens[0].Should().Be(new ItemFeed("id-1", "Governo anuncia & detalha plano",
            new DateTimeOffset(2026, 10, 14, 11, 30, 0, TimeSpan.FromHours(-3)), new Uri("https://a.test/img/1.jpg")));
        itens[1].Identificador.Should().Be("https://a.test/noticia/2", "sem guid, o link identifica");
        itens[1].PublicadaEm.Should().Be(new DateTimeOffset(2026, 10, 14, 13, 0, 0, TimeSpan.Zero));
        itens[1].Imagem.Should().Be(new Uri("https://a.test/img/2.png"));
        itens[2].Imagem.Should().Be(new Uri("https://a.test/img/3.jpg?w=800"), "endereço relativo vira absoluto");
        itens[2].PublicadaEm.Should().Be(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.FromHours(-3)));
        itens[3].Should().Be(new ItemFeed("id-4", "Sem imagem nem data", null, null));
    }

    [Fact]
    public void LeitorFeed_LeAtom()
    {
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>Blog</title>
              <entry>
                <title type="html">Entrada &lt;em&gt;um&lt;/em&gt;</title>
                <id>tag:a.test,2026:1</id>
                <link rel="alternate" href="https://a.test/1"/>
                <link rel="enclosure" type="image/jpeg" href="https://a.test/1.jpg"/>
                <published>2026-10-14T12:00:00-03:00</published>
              </entry>
            </feed>
            """;

        var item = LeitorFeed.Ler(Encoding.UTF8.GetBytes(atom), new Uri("https://a.test/atom")).Single();

        item.Should().Be(new ItemFeed("tag:a.test,2026:1", "Entrada um",
            new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-3)), new Uri("https://a.test/1.jpg")));
    }

    [Theory]
    [InlineData("<html><body>página</body></html>")]
    [InlineData("isto não é xml")]
    public void LeitorFeed_RecusaOQueNaoEFeed(string conteudo)
    {
        var acao = () => LeitorFeed.Ler(Encoding.UTF8.GetBytes(conteudo), new Uri("https://a.test/"));

        acao.Should().Throw<FormatException>();
    }

    [Fact]
    public void LeitorFeed_NaoResolveEntidadesExternas()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY segredo SYSTEM "file:///etc/passwd">]>
            <rss version="2.0"><channel><item><title>Oi &segredo;</title><guid>1</guid></item></channel></rss>
            """;

        var acao = () => LeitorFeed.Ler(Encoding.UTF8.GetBytes(xxe), new Uri("https://a.test/"));

        // Com o DTD ignorado, a entidade não existe: ou falha, ou fica sem o conteúdo do arquivo.
        try
        {
            acao().Should().OnlyContain(i => !i.Titulo.Contains("root"));
        }
        catch (FormatException)
        {
        }
    }

    // ---------- Coleta de notícias ----------

    [Fact]
    public async Task Coletar_GuardaNovasComImagemNoStorageSemRepetir()
    {
        var fonte = await CriarFonteAsync();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(Rss);
        _cliente.Respostas["https://a.test/img/1.jpg"] = Jpeg;
        _cliente.Respostas["https://a.test/img/2.png"] = Gif; // diz PNG, mas não é: fica sem imagem
        // A imagem 3 não responde: fica sem imagem.

        var resultado = await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);

        resultado.Should().Be(new ResultadoColetaNoticias(4, 0, null));
        var noticias = await _context.Noticias.OrderByDescending(n => n.PublicadaEm).ToListAsync();
        noticias.Select(n => n.Titulo).Should().BeEquivalentTo("Sem imagem nem data", "Com media:content", "Governo anuncia & detalha plano", "Imagem no texto");
        noticias.Single(n => n.Identificador == "id-4").PublicadaEm.Should().Be(Agora, "sem data, vale a hora da coleta");

        var comFoto = noticias.Single(n => n.Identificador == "id-1");
        comFoto.ChaveImagem.Should().MatchRegex($"^noticias/{fonte.Id}/[0-9a-f]{{64}}\\.jpg$");
        comFoto.TipoConteudoImagem.Should().Be("image/jpeg");
        comFoto.TamanhoImagemBytes.Should().Be(Jpeg.Length);
        _armazenamento.Arquivos[comFoto.ChaveImagem!].Should().Equal(Jpeg);
        noticias.Count(n => n.ChaveImagem != null).Should().Be(1);

        (await _context.FontesNoticia.SingleAsync()).Should().Match<FonteNoticia>(f => f.UltimaColetaEm == Agora && f.UltimoErro == null);

        var segunda = await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);
        segunda.Novas.Should().Be(0);
        (await _context.Noticias.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Coletar_IgnoraNoticiasMaisVelhasQue48Horas()
    {
        var fonte = await CriarFonteAsync();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(RssCom(("antiga", Agora.AddHours(-49)), ("recente", Agora.AddHours(-47))));

        await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);

        (await _context.Noticias.Select(n => n.Identificador).ToListAsync()).Should().Equal("recente");
    }

    [Fact]
    public async Task Coletar_FeedForaDoArRegistraOErroEMantemAsNoticias()
    {
        var fonte = await CriarFonteAsync();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(Rss);
        await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);
        _cliente.Respostas.Remove("https://a.test/rss");

        var resultado = await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);

        resultado.Erro.Should().Contain("não respondeu");
        (await _context.FontesNoticia.SingleAsync()).UltimoErro.Should().Contain("não respondeu");
        (await _context.Noticias.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Coletar_GuardaSoAsMaisRecentesEApagaOsArquivosDasOutras()
    {
        var fonte = await CriarFonteAsync();
        var primeira = Enumerable.Range(0, FonteNoticia.NoticiasGuardadas)
            .Select(i => ($"n{i}", Agora.AddHours(-40).AddMinutes(i)))
            .ToArray();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(RssCom(primeira, imagem: true));
        _cliente.Respostas["https://a.test/img.jpg"] = Jpeg;
        await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);
        var chaveCompartilhada = (await _context.Noticias.FirstAsync()).ChaveImagem!;

        // Chegam 5 mais novas, sem imagem: as 5 mais antigas saem; a imagem
        // (a mesma em todas) só sai quando nenhuma notícia usa mais.
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(RssCom(
            Enumerable.Range(0, 5).Select(i => ($"nova{i}", Agora.AddMinutes(-i))).ToArray()));

        var resultado = await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);

        resultado.Should().Be(new ResultadoColetaNoticias(5, 5, null));
        (await _context.Noticias.CountAsync()).Should().Be(FonteNoticia.NoticiasGuardadas);
        (await _context.Noticias.AnyAsync(n => n.Identificador == "n0")).Should().BeFalse();
        _armazenamento.Removidos.Should().BeEmpty("as outras notícias ainda usam a imagem");
        _armazenamento.Arquivos.Should().ContainKey(chaveCompartilhada);
    }

    [Fact]
    public async Task ExcluirFonte_ApagaNoticiasEImagens()
    {
        var fonte = await CriarFonteAsync();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes(Rss);
        _cliente.Respostas["https://a.test/img/1.jpg"] = Jpeg;
        await Coletor().ColetarAsync(fonte.Id, CancellationToken.None);
        var chave = (await _context.Noticias.SingleAsync(n => n.ChaveImagem != null)).ChaveImagem!;

        await new ExcluirFonteNoticiaCommandHandler(_context, Coletor()).Handle(new ExcluirFonteNoticiaCommand(fonte.Id), CancellationToken.None);

        (await _context.FontesNoticia.AnyAsync()).Should().BeFalse();
        (await _context.Noticias.AnyAsync()).Should().BeFalse();
        _armazenamento.Removidos.Should().Equal(chave);
    }

    // ---------- Cadastro das fontes ----------

    [Fact]
    public async Task CriarFonte_NomeEFeedSaoUnicos()
    {
        await CriarFonteAsync();
        var handler = new CriarFonteNoticiaCommandHandler(_context);

        var mesmoNome = () => handler.Handle(new CriarFonteNoticiaCommand { Nome = " agência ", UrlFeed = "https://b.test/rss", Credito = "x" }, CancellationToken.None);
        (await mesmoNome.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Nome");

        var mesmoFeed = () => handler.Handle(new CriarFonteNoticiaCommand { Nome = "Outra", UrlFeed = "https://a.test/rss", Credito = "x" }, CancellationToken.None);
        (await mesmoFeed.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("UrlFeed");
    }

    [Theory]
    [InlineData("https://a.test/rss", true)]
    [InlineData("http://a.test/rss", true)]
    [InlineData("ftp://a.test/rss", false)]
    [InlineData("a.test/rss", false)]
    public void ValidadorFonte_ExigeEnderecoHttp(string url, bool valido)
    {
        new CriarFonteNoticiaCommandValidator()
            .Validate(new CriarFonteNoticiaCommand { Nome = "A", UrlFeed = url, Credito = "Fonte: A" })
            .IsValid.Should().Be(valido);
    }

    [Fact]
    public async Task ColetarAgora_DevolveOResultado()
    {
        var fonte = await CriarFonteAsync();
        _cliente.Respostas["https://a.test/rss"] = Encoding.UTF8.GetBytes("<html/>");

        var resultado = await new ColetarFonteNoticiaCommandHandler(_context, Coletor())
            .Handle(new ColetarFonteNoticiaCommand(fonte.Id), CancellationToken.None);

        resultado.Erro.Should().Be("O endereço não é um feed RSS ou Atom.");
    }

    // ---------- Clima ----------

    private const string Geo = """
        {"results":[
          {"name":"São José","latitude":-27.6,"longitude":-48.6,"admin1":"Santa Catarina","country_code":"BR"},
          {"name":"São José","latitude":-10.1,"longitude":-37.0,"admin1":"Sergipe","country_code":"BR"}
        ]}
        """;

    private const string Previsao = """
        {"current":{"time":"2026-10-14T12:00","temperature_2m":22.46,"weather_code":61},
         "daily":{"time":["2026-10-14"],"temperature_2m_max":[25.04],"temperature_2m_min":[17.5]}}
        """;

    [Theory]
    [InlineData("São José - SE", "São José", "SE")]
    [InlineData("São José/sc", "São José", "SC")]
    [InlineData("São José, SC", "São José", "SC")]
    [InlineData("Manaus", "Manaus", null)]
    [InlineData("Rio - XX", "Rio - XX", null)]
    public void Clima_SeparaAUfDaCidade(string cidade, string nome, string? uf)
    {
        ColetorClima.SepararUf(cidade).Should().Be((nome, uf));
    }

    [Fact]
    public async Task Clima_GeocodificaPelaUfELeAPrevisao()
    {
        var host = await CriarHostComTelaAsync("São José - SE");
        _cliente.Respostas["https://geo.test/v1/search?name=S%C3%A3o%20Jos%C3%A9&count=10&language=pt&format=json&countryCode=BR"] = Encoding.UTF8.GetBytes(Geo);
        _cliente.Respostas["https://clima.test/v1/forecast?latitude=-10.1&longitude=-37&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1"] = Encoding.UTF8.GetBytes(Previsao);

        await Clima().AtualizarTodosAsync(CancellationToken.None);

        var previsao = await _context.PrevisoesClima.SingleAsync();
        previsao.Should().BeEquivalentTo(new
        {
            ParceiroId = host.Id,
            CidadeConsultada = "São José - SE",
            Local = "São José, Sergipe",
            Latitude = -10.1,
            Longitude = -37.0,
            TemperaturaC = 22.5m,
            MaximaC = 25.0m,
            MinimaC = 17.5m,
            CodigoTempo = 61,
            AtualizadaEm = (DateTimeOffset?)Agora,
            UltimoErro = (string?)null,
        });

        // Segunda atualização: não geocodifica de novo.
        _cliente.Pedidos.Clear();
        await Clima().AtualizarTodosAsync(CancellationToken.None);
        _cliente.Pedidos.Should().ContainSingle().Which.Should().StartWith("https://clima.test/");
    }

    [Fact]
    public async Task Clima_CidadeNaoEncontradaRegistraOErro()
    {
        await CriarHostComTelaAsync("Cidade Inventada");
        _cliente.Respostas["https://geo.test/v1/search?name=Cidade%20Inventada&count=10&language=pt&format=json&countryCode=BR"] = Encoding.UTF8.GetBytes("{}");

        await Clima().AtualizarTodosAsync(CancellationToken.None);

        var previsao = await _context.PrevisoesClima.SingleAsync();
        previsao.UltimoErro.Should().Contain("Cidade Inventada").And.Contain("não encontrada");
        previsao.TemperaturaC.Should().BeNull();
    }

    [Fact]
    public async Task Clima_SoParaHostsComTelaAtivaECidade()
    {
        await CriarHostComTelaAsync(null);
        await CriarHostComTelaAsync("Manaus", telaAtiva: false);

        await Clima().AtualizarTodosAsync(CancellationToken.None);

        _cliente.Pedidos.Should().BeEmpty();
        (await _context.PrevisoesClima.AnyAsync()).Should().BeFalse();
    }

    // ---------- Apoio ----------

    private ColetorNoticias Coletor() =>
        new(_context, _armazenamento, _cliente, _relogio, NullLogger<ColetorNoticias>.Instance);

    private ColetorClima Clima() =>
        new(_context, _cliente, new ConfiguracaoIndoorFake(), _relogio, NullLogger<ColetorClima>.Instance);

    private async Task<FonteNoticia> CriarFonteAsync()
    {
        var fonte = new FonteNoticia { Nome = "Agência", UrlFeed = "https://a.test/rss", Credito = "Fonte: Agência" };
        _context.FontesNoticia.Add(fonte);
        await _context.SaveChangesAsync();
        return fonte;
    }

    private async Task<Parceiro> CriarHostComTelaAsync(string? cidade, bool telaAtiva = true)
    {
        var host = new Parceiro { Nome = $"Host {Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@h.test", Cidade = cidade };
        _context.Parceiros.Add(host);
        _context.Telas.Add(new Tela { Parceiro = host, Nome = "TV", Ativo = telaAtiva });
        await _context.SaveChangesAsync();
        return host;
    }

    private static string RssCom((string Id, DateTimeOffset Data)[] itens, bool imagem = false)
    {
        var corpo = string.Concat(itens.Select(i =>
            $"<item><title>Título {i.Id}</title><guid>{i.Id}</guid><pubDate>{i.Data.ToUniversalTime():ddd, dd MMM yyyy HH:mm:ss} GMT</pubDate>"
            + (imagem ? "<enclosure url=\"https://a.test/img.jpg\" type=\"image/jpeg\"/>" : string.Empty)
            + "</item>"));
        return $"<rss version=\"2.0\"><channel><title>A</title>{corpo}</channel></rss>";
    }

    private static string RssCom(params (string Id, DateTimeOffset Data)[] itens) => RssCom(itens, imagem: false);

    /// <summary>Responde pelo endereço exato; o que não está no dicionário "não responde".</summary>
    private sealed class ClienteFake : IClienteConteudoExterno
    {
        public Dictionary<string, byte[]> Respostas { get; } = [];
        public List<string> Pedidos { get; } = [];

        public Task<ConteudoBaixado> BaixarAsync(Uri url, long limiteBytes, CancellationToken cancellationToken)
        {
            Pedidos.Add(url.AbsoluteUri);

            if (!Respostas.TryGetValue(url.AbsoluteUri, out var bytes))
            {
                throw new ConteudoExternoIndisponivelException($"{url.Host} não respondeu (teste).");
            }

            if (bytes.Length > limiteBytes)
            {
                throw new ConteudoExternoIndisponivelException("Maior que o limite.");
            }

            return Task.FromResult(new ConteudoBaixado(bytes, null));
        }
    }
}
