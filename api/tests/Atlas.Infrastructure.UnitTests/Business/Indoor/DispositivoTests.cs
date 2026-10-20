using Atlas.Application.Business.Dispositivo.Exibicoes;
using Atlas.Domain.Business.Parceiros.Entities;
using System.IdentityModel.Tokens.Jwt;
using Atlas.Application.Business.Dispositivo.Autenticacao;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Application.Business.Dispositivo.Sincronizacao;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Infrastructure.Business.Dispositivo;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Pareamento, credencial e sincronização da TV.</summary>
public class DispositivoTests : IDisposable
{
    // Segunda-feira, 12h em Manaus (16h UTC).
    private static readonly DateTimeOffset Agora = new(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 12);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly RelogioFixo _relogio = new(Agora);
    private readonly GeradorTokenFake _gerador = new();
    private readonly Cliente _cliente;
    private readonly Parceiro _parceiro;
    private readonly Tela _tela;

    public DispositivoTests()
    {
        _cliente = new Cliente { Nome = "Academia Centro", Email = "a@teste.local", FusoHorario = "America/Manaus" };
        _context.Clientes.Add(_cliente);
        _parceiro = new Parceiro { Nome = "Academia Centro", FusoHorario = "America/Manaus" };
        _context.Parceiros.Add(_parceiro);
        _tela = new Tela { Parceiro = _parceiro, Nome = "Recepção" };
        _tela.GerarCodigoPareamento("AB12CD34", Agora.AddHours(-1));
        _context.Telas.Add(_tela);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ---------- Pareamento ----------

    [Fact]
    public async Task Autenticar_DeveParearConsumirOCodigoEDevolverToken()
    {
        var resultado = await Autenticar(" ab12-cd34 ", "tv-uuid-1", "1.0.0");

        resultado.TelaId.Should().Be(_tela.Id);
        resultado.NomeTela.Should().Be("Recepção");
        resultado.NomeParceiro.Should().Be("Academia Centro");
        resultado.IntervaloSincronizacaoSegundos.Should().Be(300);
        resultado.Token.Should().Be(_gerador.Ultimo);

        var tela = await _context.Telas.Include(t => t.Estado).SingleAsync();
        tela.DispositivoId.Should().Be("tv-uuid-1");
        tela.CodigoPareamento.Should().BeNull();
        tela.Estado!.VersaoApp.Should().Be("1.0.0");
        tela.Estado.UltimaSincronizacao.Should().Be(Agora);
        _gerador.Emitidos.Single().Should().Be((tela.Id, _parceiro.Id, "tv-uuid-1", tela.VersaoCredencial));
    }

    [Fact]
    public async Task Autenticar_CodigoUsadoDuasVezesDeveSerRecusado()
    {
        await Autenticar("AB12CD34", "tv-1");

        var acao = () => Autenticar("AB12CD34", "tv-2");

        await acao.Should().ThrowAsync<ValidationException>();
        (await _context.Telas.SingleAsync()).DispositivoId.Should().Be("tv-1");
    }

    [Fact]
    public async Task Autenticar_CodigoExpiradoDeveSerRecusado()
    {
        _relogio.Agora = Agora.AddHours(24);

        var acao = () => Autenticar("AB12CD34", "tv-1");

        (await acao.Should().ThrowAsync<ValidationException>())
            .Which.Errors["CodigoPareamento"].Should().Equal(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);
    }

    [Fact]
    public async Task Autenticar_TelaInativaDaAMesmaRespostaQueCodigoErrado()
    {
        _tela.Ativo = false;
        await _context.SaveChangesAsync();
        var inativa = () => Autenticar("AB12CD34", "tv-1");
        (await inativa.Should().ThrowAsync<ValidationException>())
            .Which.Errors["CodigoPareamento"].Should().Equal(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);
    }

    [Fact]
    public async Task Autenticar_HostBloqueadoNaoImpedeOPareamento()
    {
        // A TV é da operadora: o host bloqueado não tira a tela do ar.
        _cliente.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();

        (await Autenticar("AB12CD34", "tv-1")).TelaId.Should().Be(_tela.Id);
    }

    [Fact]
    public async Task Autenticar_NovaTvNoLugarDeveRevogarAAnterior()
    {
        await Autenticar("AB12CD34", "tv-velha");
        var versaoVelha = _tela.VersaoCredencial;
        _tela.GerarCodigoPareamento("ZZ99YY88", Agora);
        await _context.SaveChangesAsync();

        await Autenticar("ZZ99YY88", "tv-nova");

        var antiga = () => Atual(new CredencialFake(_tela.Id, _parceiro.Id, "tv-velha", versaoVelha)).ObterAsync(CancellationToken.None);
        await antiga.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("ab12cd3", false)]
    [InlineData("AB12-CD34", true)]
    [InlineData("ab 12 cd 34", true)]
    [InlineData("", false)]
    public void Validador_ConfereOTamanhoDoCodigoNormalizado(string codigo, bool valido)
    {
        new AutenticarDispositivoCommandValidator()
            .Validate(new AutenticarDispositivoCommand { CodigoPareamento = codigo, DispositivoId = "tv" })
            .IsValid.Should().Be(valido);
    }

    // ---------- Credencial ----------

    [Fact]
    public async Task DispositivoAtual_DeveSeparar401De403()
    {
        await Autenticar("AB12CD34", "tv-1");
        var valida = new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial);

        (await Atual(valida).ObterAsync(CancellationToken.None)).TelaId.Should().Be(_tela.Id);

        // Token sem as claims, de outra TV ou de credencial antiga: 401 (parear de novo).
        await Esperar401(new CredencialFake(null, null, null, null));
        await Esperar401(valida with { DispositivoId = "tv-2" });
        await Esperar401(valida with { VersaoCredencial = _tela.VersaoCredencial - 1 });

        // A TV mudou de local (a operadora trocou o parceiro da tela): o token continua valendo.
        (await Atual(valida with { ParceiroId = _parceiro.Id + 1 }).ObterAsync(CancellationToken.None)).TelaId.Should().Be(_tela.Id);

        // Cliente bloqueado não suspende a TV (ela é da operadora).
        _cliente.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();
        (await Atual(valida).ObterAsync(CancellationToken.None)).TelaId.Should().Be(_tela.Id);

        // Tela inativa: 403 (continua no que baixou).
        _tela.Ativo = false;
        await _context.SaveChangesAsync();
        var inativa = () => Atual(valida).ObterAsync(CancellationToken.None);
        await inativa.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Desvincular_DeveRevogarOToken()
    {
        await Autenticar("AB12CD34", "tv-1");
        var credencial = new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial);

        _tela.Desvincular();
        await _context.SaveChangesAsync();

        await Esperar401(credencial);
    }

    // ---------- Sincronização ----------

    [Fact]
    public async Task Sincronizar_MidiaDeAnuncianteBloqueadoSaiDaGrade()
    {
        var anunciante = new Cliente { Nome = "Loja", Email = "loja@teste.local" };
        _context.Clientes.Add(anunciante);
        await _context.SaveChangesAsync();
        var daLoja = Midia("Promo da loja");
        daLoja.ClienteId = anunciante.Id;

        var credencial = await ParearComGradeAsync((Midia("Da academia"), DiasSemana.Todos), (daLoja, DiasSemana.Todos));
        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Itens!.Should().HaveCount(2);

        anunciante.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();

        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery());
        grade.Itens!.Select(i => Titulo(i.MidiaId)).Should().Equal("Da academia");
    }

    [Fact]
    public async Task Sincronizar_DeveEntregarAGradeComFiltrosEUrlsNumaChamadaSo()
    {
        var credencial = await ParearComGradeAsync(
            (Midia("Segunda a sexta"), DiasSemana.DiasUteis),
            (Midia("Só fim de semana"), DiasSemana.FimDeSemana),
            (Midia("Começa amanhã", inicio: Hoje.AddDays(1)), DiasSemana.Todos),
            (Midia("Venceu ontem", fim: Hoje.AddDays(-1)), DiasSemana.Todos),
            (Midia("Inativa", ativa: false), DiasSemana.Todos),
            (Midia("Termina hoje", fim: Hoje), DiasSemana.Todos));

        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoApp = "1.0.1", ItensProntos = 2 });

        grade.Alterada.Should().BeTrue();
        grade.Hoje.Should().Be(Hoje);
        grade.FusoHorario.Should().Be("America/Manaus");
        grade.NomeTela.Should().Be("Recepção");
        grade.Itens!.Select(i => (Titulo(i.MidiaId), i.ExibirHoje)).Should().Equal(
            ("Segunda a sexta", true),
            ("Só fim de semana", false),
            ("Começa amanhã", false),
            ("Termina hoje", true));
        grade.Itens!.Select(i => i.Ordem).Should().BeInAscendingOrder();
        grade.Itens![0].DiasSemana.Should().Be((int)DiasSemana.DiasUteis);
        grade.Itens![0].Url.ToString().Should().Contain("s=86400");
        grade.Itens![0].Hash.Should().HaveLength(64);
        _armazenamento.ChamadasEmLote.Should().Be(1);

        var estado = await _context.EstadosTela.SingleAsync();
        estado.UltimaSincronizacao.Should().Be(Agora);
        estado.VersaoApp.Should().Be("1.0.1");
        estado.ItensProntos.Should().Be(2);
        estado.VersaoGrade.Should().Be(grade.VersaoGrade);
    }

    [Fact]
    public async Task Sincronizar_ComAMesmaVersaoNaoDeveMandarItensNemGerarUrls()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var primeira = await Sincronizar(credencial, new GetSincronizacaoQuery());

        var segunda = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = primeira.VersaoGrade });

        segunda.Alterada.Should().BeFalse();
        segunda.Itens.Should().BeNull();
        segunda.VersaoGrade.Should().Be(primeira.VersaoGrade);
        _armazenamento.ChamadasEmLote.Should().Be(1, "a segunda sincronização não precisa de URLs");
    }

    [Fact]
    public async Task Sincronizar_MudarAGradeDeveMudarAVersao()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var antes = await Sincronizar(credencial, new GetSincronizacaoQuery());

        var item = await _context.ItensPlaylist.SingleAsync();
        item.DiasSemana = DiasSemana.Segunda;
        await _context.SaveChangesAsync();

        var depois = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = antes.VersaoGrade });

        depois.Alterada.Should().BeTrue();
        depois.VersaoGrade.Should().NotBe(antes.VersaoGrade);
        depois.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task Sincronizar_PlaylistInativaOuAusenteDaGradeVazia()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var playlist = await _context.Playlists.SingleAsync();
        playlist.Ativo = false;
        await _context.SaveChangesAsync();

        var inativa = await Sincronizar(credencial, new GetSincronizacaoQuery());
        inativa.Itens.Should().BeEmpty();

        _tela.PlaylistId = null;
        await _context.SaveChangesAsync();
        var semPlaylist = await Sincronizar(credencial, new GetSincronizacaoQuery());
        semPlaylist.Itens.Should().BeEmpty();
        semPlaylist.VersaoGrade.Should().NotBe(inativa.VersaoGrade, "sem playlist é outra grade, mesmo vazia");
    }

    [Fact]
    public async Task Sincronizar_DeveRenovarOTokenPertoDeVencer()
    {
        var credencial = await ParearComGradeAsync();

        (await Sincronizar(credencial with { ExpiraEm = Agora.AddDays(20) }, new GetSincronizacaoQuery()))
            .NovoToken.Should().BeNull();

        var renovada = await Sincronizar(credencial with { ExpiraEm = Agora.AddDays(5) }, new GetSincronizacaoQuery());
        renovada.NovoToken.Should().NotBeNull();
        renovada.NovoToken!.Token.Should().Be(_gerador.Ultimo);
    }

    [Fact]
    public async Task Sincronizar_HojeEOdoFusoDoCliente()
    {
        var credencial = await ParearComGradeAsync((Midia("Segunda"), DiasSemana.Segunda));

        // 02h UTC de terça ainda é segunda (22h) em Manaus.
        _relogio.Agora = new DateTimeOffset(2026, 10, 13, 2, 0, 0, TimeSpan.Zero);
        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery());

        grade.Hoje.Should().Be(Hoje);
        grade.Itens!.Single().ExibirHoje.Should().BeTrue();
    }

    [Fact]
    public async Task Sincronizar_EntregaAComposicaoDaPlaylistEAPosicaoFixa()
    {
        var credencial = await ParearComGradeAsync((Midia("Da casa"), DiasSemana.Todos), (Midia("Anúncio"), DiasSemana.Todos));
        var playlist = await _context.Playlists.Include(p => p.Itens).SingleAsync();
        playlist.NoticiaACadaAnuncios = 3;
        playlist.DuracaoNoticiaSegundos = 12;
        playlist.EmbaralharAnuncios = true;
        playlist.ExibirRelogio = false;
        playlist.DuracaoMaximaVoltaSegundos = 180;
        await _context.SaveChangesAsync();

        var antes = await Sincronizar(credencial, new GetSincronizacaoQuery());
        antes.Composicao.Should().BeEquivalentTo(new ComposicaoFilaDto
        {
            NoticiaACadaAnuncios = 3, DuracaoNoticiaSegundos = 12, EmbaralharAnuncios = true, ExibirRelogio = false, ExibirClima = true,
            DuracaoMaximaVoltaSegundos = 180,
        });
        antes.Itens!.Select(i => i.PosicaoFixa).Should().Equal(false, false);

        playlist.Itens.OrderBy(i => i.Ordem).First().PosicaoFixa = true;
        await _context.SaveChangesAsync();

        var depois = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = antes.VersaoGrade });
        depois.Alterada.Should().BeTrue("fixar um item muda a grade");
        depois.Itens!.Select(i => i.PosicaoFixa).Should().Equal(true, false);
    }

    [Fact]
    public async Task Sincronizar_SemPlaylistAtivaAComposicaoEDesligada()
    {
        var credencial = await ParearComGradeAsync();
        var playlist = await _context.Playlists.SingleAsync();
        playlist.NoticiaACadaAnuncios = 2;
        playlist.Ativo = false;
        await _context.SaveChangesAsync();

        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery());

        grade.Composicao.NoticiaACadaAnuncios.Should().Be(0);
        grade.Noticias.Should().BeEmpty();
    }

    [Fact]
    public async Task Sincronizar_NoticiasRecentesDasFontesAtivasComVersaoPropria()
    {
        var credencial = await ParearComGradeAsync((Midia("Anúncio"), DiasSemana.Todos));
        var ativa = new FonteNoticia { Nome = "Agência", UrlFeed = "https://a.test/rss", Credito = "Fonte: Agência" };
        var inativa = new FonteNoticia { Nome = "Outra", UrlFeed = "https://b.test/rss", Credito = "Fonte: Outra", Ativo = false };
        _context.FontesNoticia.AddRange(ativa, inativa);
        await _context.SaveChangesAsync();

        Noticia Nova(FonteNoticia fonte, string titulo, TimeSpan idade, bool comImagem = false)
        {
            var noticia = new Noticia
            {
                FonteNoticiaId = fonte.Id, Identificador = titulo, Titulo = titulo,
                PublicadaEm = Agora - idade, ColetadaEm = Agora,
            };
            if (comImagem)
            {
                noticia.ChaveImagem = $"noticias/{fonte.Id}/{titulo}.jpg";
                noticia.HashImagem = new string('e', 64);
                noticia.TamanhoImagemBytes = 10;
                noticia.TipoConteudoImagem = "image/jpeg";
                _armazenamento.Arquivos[noticia.ChaveImagem] = [1];
            }

            return noticia;
        }

        _context.Noticias.AddRange(
            Nova(ativa, "Velha", TimeSpan.FromHours(50)),
            Nova(ativa, "Com foto", TimeSpan.FromHours(1), comImagem: true),
            Nova(ativa, "Mais nova", TimeSpan.FromMinutes(5)),
            Nova(inativa, "De fonte inativa", TimeSpan.FromMinutes(1)));
        await _context.SaveChangesAsync();

        // Playlist sem notícias: nada vai.
        var sem = await Sincronizar(credencial, new GetSincronizacaoQuery());
        sem.Noticias.Should().BeEmpty();

        var playlist = await _context.Playlists.SingleAsync();
        playlist.NoticiaACadaAnuncios = 2;
        await _context.SaveChangesAsync();

        var com = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = sem.VersaoGrade, VersaoNoticias = sem.VersaoNoticias });
        com.Noticias!.Select(n => n.Titulo).Should().Equal("Mais nova", "Com foto");
        com.Noticias![0].Url.Should().BeNull();
        com.Noticias![0].Credito.Should().Be("Fonte: Agência");
        com.Noticias![1].Url!.AbsoluteUri.Should().Contain("Com%20foto");
        com.Noticias![1].Hash.Should().Be(new string('e', 64));
        com.Itens.Should().BeNull("a grade de anúncios não mudou");

        var deNovo = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = com.VersaoGrade, VersaoNoticias = com.VersaoNoticias });
        deNovo.Noticias.Should().BeNull();
        deNovo.VersaoNoticias.Should().Be(com.VersaoNoticias);
    }

    [Fact]
    public async Task Sincronizar_MesmaMancheteEmDuasFontesVaiUmaVezSoComAFoto()
    {
        var credencial = await ParearComGradeAsync((Midia("Anúncio"), DiasSemana.Todos));
        var geral = new FonteNoticia { Nome = "Últimas", UrlFeed = "https://a.test/ultimas", Credito = "Fonte: Agência" };
        var editoria = new FonteNoticia { Nome = "Economia", UrlFeed = "https://a.test/economia", Credito = "Fonte: Agência" };
        _context.FontesNoticia.AddRange(geral, editoria);
        await _context.SaveChangesAsync();
        _context.Noticias.AddRange(
            new Noticia { FonteNoticiaId = geral.Id, Identificador = "1", Titulo = "Juros caem", PublicadaEm = Agora.AddHours(-1), ColetadaEm = Agora },
            new Noticia
            {
                FonteNoticiaId = editoria.Id, Identificador = "1", Titulo = " juros caem ", PublicadaEm = Agora.AddHours(-1), ColetadaEm = Agora,
                ChaveImagem = "noticias/2/f.jpg", HashImagem = new string('f', 64), TamanhoImagemBytes = 5, TipoConteudoImagem = "image/jpeg",
            },
            new Noticia { FonteNoticiaId = geral.Id, Identificador = "2", Titulo = "Outra", PublicadaEm = Agora.AddHours(-2), ColetadaEm = Agora });
        _armazenamento.Arquivos["noticias/2/f.jpg"] = [1];
        (await _context.Playlists.SingleAsync()).NoticiaACadaAnuncios = 1;
        await _context.SaveChangesAsync();

        var noticias = (await Sincronizar(credencial, new GetSincronizacaoQuery())).Noticias!;

        noticias.Should().HaveCount(2);
        noticias[0].Titulo.Should().Be(" juros caem ", "fica a versão com foto");
        noticias[0].Hash.Should().Be(new string('f', 64));
        noticias[1].Titulo.Should().Be("Outra");
    }

    [Fact]
    public async Task Sincronizar_CartoesDeTempoECotacoes()
    {
        var credencial = await ParearComGradeAsync((Midia("Anúncio"), DiasSemana.Todos));
        _context.PrevisoesClima.Add(new PrevisaoClima
        {
            ParceiroId = _parceiro.Id, CidadeConsultada = "Manaus", Local = "Manaus, Amazonas", Latitude = -3, Longitude = -60,
            TemperaturaC = 30m, MinimaC = 24m, MaximaC = 33m, CodigoTempo = 0, AtualizadaEm = Agora.AddMinutes(-10),
            PrevisaoDias = "[{\"Data\":\"2026-10-11\",\"MinimaC\":23,\"MaximaC\":31,\"Codigo\":3},"
                + "{\"Data\":\"2026-10-12\",\"MinimaC\":24,\"MaximaC\":33,\"Codigo\":0},"
                + "{\"Data\":\"2026-10-13\",\"MinimaC\":25,\"MaximaC\":34,\"Codigo\":95}]",
        });
        _context.Cotacoes.AddRange(
            new Cotacao { Moeda = "EUR", Compra = 6.1m, Venda = 6.12m, CotadaEm = Agora.AddHours(-1), FechamentoAnterior = 6.2m, AtualizadaEm = Agora },
            new Cotacao { Moeda = "USD", Compra = 5.5m, Venda = 5.51m, CotadaEm = Agora.AddHours(-1), FechamentoAnterior = 5.46m, AtualizadaEm = Agora });
        var playlist = await _context.Playlists.SingleAsync();
        playlist.ExibirClima = false;
        await _context.SaveChangesAsync();

        var sem = await Sincronizar(credencial, new GetSincronizacaoQuery());
        sem.Clima.Should().BeNull();
        sem.Cotacoes.Should().BeNull();

        playlist.CartaoTempo = true;
        playlist.CartaoCotacoes = true;
        await _context.SaveChangesAsync();

        var com = await Sincronizar(credencial, new GetSincronizacaoQuery());
        com.Composicao.CartaoTempo.Should().BeTrue();
        com.Clima!.Dias.Select(d => (d.Data, d.Icone)).Should().Equal(
            (new DateOnly(2026, 10, 12), "sol"), (new DateOnly(2026, 10, 13), "tempestade"));
        com.Cotacoes!.Select(c => (c.Moeda, c.Nome, c.Venda, c.VariacaoPercentual)).Should().Equal(
            ("USD", "Dólar", 5.51m, 0.92m), ("EUR", "Euro", 6.12m, -1.29m));
        com.Cotacoes![0].Credito.Should().Be("Fonte: Banco Central (PTAX)");

        // Cotação de mais de 4 dias não vai.
        foreach (var c in _context.Cotacoes) c.CotadaEm = Agora.AddDays(-5);
        await _context.SaveChangesAsync();
        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Cotacoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Sincronizar_QrCodeDaMidiaVaiNoItemEMudaAVersao()
    {
        var midia = Midia("Promo");
        var credencial = await ParearComGradeAsync((midia, DiasSemana.Todos), (Midia("Sem QR"), DiasSemana.Todos));
        var antes = await Sincronizar(credencial, new GetSincronizacaoQuery());
        antes.Itens!.Should().OnlyContain(i => i.QrCode == null);

        midia.DefinirQrCode(MontadorQrCode.Montar(new AtualizarMidiaCommand
        {
            QrTipo = TipoQrCode.Instagram, QrDestino = "@padaria", QrTamanho = TamanhoQrCode.Grande,
        })!);
        await _context.SaveChangesAsync();

        var depois = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = antes.VersaoGrade });
        depois.Alterada.Should().BeTrue("o QR code faz parte do que a TV mostra");
        depois.Itens![0].QrCode.Should().BeEquivalentTo(new QrCodeSincronizacaoDto
        {
            Conteudo = "https://instagram.com/padaria", Legenda = "Siga no Instagram",
            Posicao = PosicaoQrCode.InferiorDireito, Tamanho = TamanhoQrCode.Grande,
        });
        depois.Itens![1].QrCode.Should().BeNull();
    }

    [Fact]
    public async Task Sincronizar_ClimaDoHostRecenteEDesligavelNaPlaylist()
    {
        var credencial = await ParearComGradeAsync((Midia("Anúncio"), DiasSemana.Todos));
        var previsao = new PrevisaoClima
        {
            ParceiroId = _parceiro.Id, CidadeConsultada = "Manaus", Local = "Manaus, Amazonas",
            Latitude = -3.1, Longitude = -60, TemperaturaC = 31.4m, MinimaC = 24m, MaximaC = 33.2m, CodigoTempo = 2,
            AtualizadaEm = Agora.AddMinutes(-20),
        };
        _context.PrevisoesClima.Add(previsao);
        await _context.SaveChangesAsync();

        var clima = (await Sincronizar(credencial, new GetSincronizacaoQuery())).Clima;
        clima.Should().BeEquivalentTo(new ClimaSincronizacaoDto
        {
            TemperaturaC = 31.4m, MinimaC = 24m, MaximaC = 33.2m, Codigo = 2, Descricao = "Parcialmente nublado",
            Icone = "sol-nuvem", Local = "Manaus, Amazonas", AtualizadaEm = Agora.AddMinutes(-20),
        });

        previsao.AtualizadaEm = Agora.AddHours(-7);
        await _context.SaveChangesAsync();
        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Clima.Should().BeNull("leitura velha não vai para a TV");

        previsao.AtualizadaEm = Agora;
        (await _context.Playlists.SingleAsync()).ExibirClima = false;
        await _context.SaveChangesAsync();
        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Clima.Should().BeNull("a playlist desligou o clima");
    }

    [Fact]
    public void Versao_DependeDoConteudoEDaOrdem()
    {
        static GetSincronizacaoQueryHandler.ItemGrade Item(int ordem, long midia) =>
            new(ordem, midia, TipoMidia.Video, "video/mp4", "c", new string('a', 64), 1, 15, DiasSemana.Todos, null, null);

        var ab = GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 1), Item(1, 2)]);

        ab.Should().HaveLength(64)
            .And.Be(GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 1), Item(1, 2)]));
        ab.Should().NotBe(GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 2), Item(1, 1)]));
        ab.Should().NotBe(GetSincronizacaoQueryHandler.CalcularVersao(2, [Item(0, 1), Item(1, 2)]));
    }

    // ---------- Token de verdade ----------

    [Fact]
    public void Token_ValeSoNoEsquemaDaTv()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Atlas",
                ["Jwt:Key"] = new string('k', 64),
            })
            .Build();
        var token = new GeradorTokenDispositivo(configuracao, new ConfiguracaoIndoorFake(), TimeProvider.System).Gerar(7, 3, "tv-1", 2);
        var (emissor, chave) = AutenticacaoDispositivo.LerChave(configuracao);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var principal = handler.ValidateToken(token.Token, new TokenValidationParameters
        {
            ValidIssuer = emissor,
            ValidAudience = AutenticacaoDispositivo.Audiencia,
            IssuerSigningKey = chave,
        }, out _);

        principal.FindFirst(ClaimsDispositivo.TelaId)!.Value.Should().Be("7");
        principal.FindFirst(ClaimsDispositivo.ParceiroId)!.Value.Should().Be("3");
        principal.FindFirst(ClaimsDispositivo.DispositivoId)!.Value.Should().Be("tv-1");
        principal.FindFirst(ClaimsDispositivo.VersaoCredencial)!.Value.Should().Be("2");
        token.ExpiraEm.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));

        // Na audiência dos usuários da plataforma o token não vale.
        var comoUsuario = () => handler.ValidateToken(token.Token, new TokenValidationParameters
        {
            ValidIssuer = emissor,
            ValidAudience = "Atlas.Clients",
            IssuerSigningKey = chave,
        }, out _);
        comoUsuario.Should().Throw<SecurityTokenInvalidAudienceException>();
    }

    // ---------- Apoio ----------

    private async Task<DispositivoAutenticadoDto> Autenticar(string codigo, string dispositivo, string? versaoApp = null) =>
        await new AutenticarDispositivoCommandHandler(_context, _gerador, new ConfiguracaoIndoorFake(), _relogio)
            .Handle(new AutenticarDispositivoCommand { CodigoPareamento = codigo, DispositivoId = dispositivo, VersaoApp = versaoApp }, CancellationToken.None);

    private DispositivoAtual Atual(CredencialFake credencial) => new(credencial, _context);

    private async Task Esperar401(CredencialFake credencial)
    {
        var acao = () => Atual(credencial).ObterAsync(CancellationToken.None);
        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private Task<SincronizacaoDto> Sincronizar(CredencialFake credencial, GetSincronizacaoQuery query, ConfiguracaoIndoorFake? configuracao = null) =>
        new GetSincronizacaoQueryHandler(
                _context, Atual(credencial), _armazenamento, _gerador, configuracao ?? new ConfiguracaoIndoorFake(), _relogio,
                NullLogger<GetSincronizacaoQueryHandler>.Instance)
            .Handle(query, CancellationToken.None);

    [Fact]
    public async Task Sincronizar_DiretorioLigadoVemComOEnderecoPublico()
    {
        var credencial = await ParearComGradeAsync((Midia("Promo"), DiasSemana.Todos));

        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Diretorio.Should().BeNull("o diretório nasce desligado");

        _tela.DefinirCodigoDiretorio("ABCDEFGH23");
        _tela.DiretorioAtivo = true;
        await _context.SaveChangesAsync();
        var ligado = await Sincronizar(credencial, new GetSincronizacaoQuery());
        ligado.Diretorio!.Url.Should().Be("https://tv.indoor.test/d/ABCDEFGH23");
        ligado.Diretorio.Legenda.Should().Be(DiretorioSincronizacaoDto.LegendaPadrao);

        // Mudar o diretório não reenvia a grade.
        var mesmaGrade = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = ligado.VersaoGrade });
        mesmaGrade.Alterada.Should().BeFalse();
        mesmaGrade.Diretorio.Should().NotBeNull();

        var semUrlPublica = await Sincronizar(credencial, new GetSincronizacaoQuery(), new ConfiguracaoIndoorFake { UrlPublica = null });
        semUrlPublica.Diretorio.Should().BeNull("sem endereço público a TV não teria o que mostrar");
    }

    [Fact]
    public async Task Sincronizar_AtualizacaoDoAppSoParaQuemTemVersaoMaisAntigaELiberada()
    {
        var credencial = await ParearComGradeAsync((Midia("Promo"), DiasSemana.Todos));
        _context.VersoesApp.AddRange(
            new VersaoApp { Codigo = 2, Nome = "1.1.0", ChaveArmazenamento = "app/v2.apk", TamanhoBytes = 10, Hash = new string('2', 64), Distribuicao = DistribuicaoVersao.Todas },
            new VersaoApp { Codigo = 3, Nome = "1.2.0", ChaveArmazenamento = "app/v3.apk", TamanhoBytes = 10, Hash = new string('3', 64), Distribuicao = DistribuicaoVersao.Teste },
            new VersaoApp { Codigo = 4, Nome = "1.3.0", ChaveArmazenamento = "app/v4.apk", TamanhoBytes = 10, Hash = new string('4', 64), Distribuicao = DistribuicaoVersao.Suspensa });
        await _context.SaveChangesAsync();

        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Atualizacao.Should().BeNull("app antigo não informa o código");

        var v1 = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoCodigoApp = 1 });
        v1.Atualizacao!.Codigo.Should().Be(2, "a de teste e a suspensa não vão para TV comum");
        v1.Atualizacao.Hash.Should().Be(new string('2', 64));
        v1.Atualizacao.Url.Should().NotBeNull();
        (await _context.EstadosTela.SingleAsync()).VersaoCodigoApp.Should().Be(1);

        (await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoCodigoApp = 2 })).Atualizacao.Should().BeNull("já está na mais nova liberada");

        _tela.TvDeTeste = true;
        await _context.SaveChangesAsync();
        (await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoCodigoApp = 2 })).Atualizacao!.Codigo.Should().Be(3, "TV de teste recebe a de teste");
    }

    [Fact]
    public async Task Sincronizar_QrRastreadoVaiComOLinkCurtoDaApi()
    {
        var midia = Midia("Promo");
        var credencial = await ParearComGradeAsync((midia, DiasSemana.Todos));
        midia.DefinirQrCode(MontadorQrCode.Montar(new AtualizarMidiaCommand { QrTipo = TipoQrCode.WhatsApp, QrDestino = "92999990000" })!);
        midia.DefinirCodigoQr("ABCD2345");
        await _context.SaveChangesAsync();

        (await Sincronizar(credencial, new GetSincronizacaoQuery())).Itens![0].QrCode!.Conteudo
            .Should().Be("https://tv.indoor.test/q/ABCD2345", "a TV mostra o link que conta a leitura");

        (await Sincronizar(credencial, new GetSincronizacaoQuery(), new ConfiguracaoIndoorFake { UrlPublica = null })).Itens![0].QrCode!.Conteudo
            .Should().Be("https://wa.me/5592999990000", "sem endereço público vai o destino direto");
    }

    // ---------- Exibições (prova de veiculação) ----------

    [Fact]
    public async Task Exibicoes_GravaComClienteEParceiro_ReenvioNaoDuplica()
    {
        var midia = Midia("Promo");
        var credencial = await ParearComGradeAsync((midia, DiasSemana.Todos));
        var lote = new RegistrarExibicoesCommand(
        [
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora.AddMinutes(-30), 15_000, Concluida: true, SemConexao: false),
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora.AddMinutes(-10), 4_000, Concluida: false, SemConexao: true),
        ]);

        (await Exibicoes(credencial, lote)).Should().Be(new RegistroExibicoesDto(2, 0, 0));
        (await Exibicoes(credencial, lote)).Should().Be(new RegistroExibicoesDto(0, 2, 0), "a TV reenviou o mesmo lote");

        var gravadas = await _context.RegistrosExibicao.OrderBy(r => r.Inicio).ToListAsync();
        gravadas.Should().HaveCount(2);
        gravadas.Should().OnlyContain(r => r.TelaId == _tela.Id && r.ParceiroId == _parceiro.Id && r.ClienteId == _cliente.Id && r.RecebidoEm == Agora);
        gravadas[1].Should().BeEquivalentTo(new { DuracaoMs = 4_000, Concluida = false, SemConexao = true });
    }

    [Fact]
    public async Task Exibicoes_DescartaMidiaDesconhecidaDataForaDaJanelaEDuracaoInvalida()
    {
        var midia = Midia("Promo");
        var credencial = await ParearComGradeAsync((midia, DiasSemana.Todos));
        var repetido = Guid.NewGuid();

        var resultado = await Exibicoes(credencial, new RegistrarExibicoesCommand(
        [
            new ExibicaoInformada(Guid.NewGuid(), 999_999, Agora, 1000, true, false),
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora.AddDays(-40), 1000, true, false),
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora.AddHours(1), 1000, true, false),
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora, -1, true, false),
            new ExibicaoInformada(Guid.NewGuid(), midia.Id, Agora, 3 * 3600 * 1000, true, false),
            new ExibicaoInformada(Guid.Empty, midia.Id, Agora, 1000, true, false),
            new ExibicaoInformada(repetido, midia.Id, Agora, 1000, true, false),
            new ExibicaoInformada(repetido, midia.Id, Agora, 1000, true, false),
        ]));

        resultado.Should().Be(new RegistroExibicoesDto(Aceitas: 1, JaRecebidas: 1, Descartadas: 6));
    }

    [Fact]
    public async Task Exibicoes_LoteGrandeDemaisERecusado()
    {
        var itens = Enumerable.Range(0, RegistrarExibicoesCommandValidator.TamanhoMaximoLote + 1)
            .Select(_ => new ExibicaoInformada(Guid.NewGuid(), 1, Agora, 1, true, false)).ToList();

        (await new RegistrarExibicoesCommandValidator().ValidateAsync(new RegistrarExibicoesCommand(itens))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Exibicoes_TvDesvinculadaRecebe401()
    {
        var midia = Midia("Promo");
        var credencial = await ParearComGradeAsync((midia, DiasSemana.Todos));
        _tela.Desvincular();
        await _context.SaveChangesAsync();

        var acao = () => Exibicoes(credencial, new RegistrarExibicoesCommand([]));
        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private Task<RegistroExibicoesDto> Exibicoes(CredencialFake credencial, RegistrarExibicoesCommand comando) =>
        new RegistrarExibicoesCommandHandler(_context, Atual(credencial), _relogio).Handle(comando, CancellationToken.None);

    /// <summary>Pareia a TV e põe a playlist com os itens informados na tela.</summary>
    private async Task<CredencialFake> ParearComGradeAsync(params (Midia Midia, DiasSemana Dias)[] itens)
    {
        foreach (var (midia, _) in itens)
        {
            _context.Midias.Add(midia);
            _armazenamento.Arquivos[midia.ChaveArmazenamento] = [1];
        }

        await _context.SaveChangesAsync();

        var playlist = new Playlist { Nome = "Grade" };
        playlist.DefinirItens(itens.Select(i => new ItemPlaylistInformado(null, i.Midia.Id, i.Dias)).ToList());
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();

        _tela.PlaylistId = playlist.Id;
        await _context.SaveChangesAsync();

        await Autenticar("AB12CD34", "tv-1");
        return new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial, Agora.AddDays(30));
    }

    private Midia Midia(string titulo, DateOnly? inicio = null, DateOnly? fim = null, bool ativa = true) => new()
    {
        ClienteId = _cliente.Id,
        Titulo = titulo,
        Tipo = TipoMidia.Video,
        ChaveArmazenamento = ChavesArmazenamento.Gerar(_cliente.Id, TiposArquivoMidia.Mp4),
        TipoConteudo = TiposArquivoMidia.Mp4,
        TamanhoBytes = 1000,
        Hash = new string('c', 64),
        InicioExibicao = inicio,
        FimExibicao = fim,
        Ativo = ativa,
    };

    private string Titulo(long midiaId) => _context.Midias.Single(m => m.Id == midiaId).Titulo;

    private sealed record CredencialFake(
        long? TelaId,
        long? ParceiroId,
        string? DispositivoId,
        int? VersaoCredencial,
        DateTimeOffset? ExpiraEm = null) : ICredencialDispositivo;

    private sealed class GeradorTokenFake : IGeradorTokenDispositivo
    {
        public List<(long TelaId, long ParceiroId, string DispositivoId, int Versao)> Emitidos { get; } = [];
        public string? Ultimo { get; private set; }

        public TokenDispositivo Gerar(long telaId, long parceiroId, string dispositivoId, int versaoCredencial)
        {
            Emitidos.Add((telaId, parceiroId, dispositivoId, versaoCredencial));
            Ultimo = $"token-{Emitidos.Count}";
            return new TokenDispositivo(Ultimo, DateTimeOffset.UnixEpoch);
        }
    }
}
