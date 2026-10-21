using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Application.Business.Diretorio;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Clientes.Perfil;
using Atlas.Application.Business.Rede.Telas.Commands.Pareamento;
using Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;
using Atlas.Application.Business.Rede.Telas.Queries;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.Business.Diretorio;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Diretório de anunciantes da TV (ADR-018): dupla autorização, perfil público e página.</summary>
public class DiretorioTests : IDisposable
{
    // Segunda-feira, 12h em Manaus.
    private static readonly DateTimeOffset Agora = new(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 12);
    private const string Codigo = "ABCDEFGH23";

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly RelogioFixo _relogio = new(Agora);
    private readonly Cliente _host;
    private readonly Parceiro _local;
    private readonly Tela _tela;
    private readonly Playlist _playlist;

    public DiretorioTests()
    {
        _host = new Cliente { Nome = "Café Central", Email = "cafe@teste.local", FusoHorario = "America/Manaus", Categoria = "Cafeteria" };
        _context.Clientes.Add(_host);
        // O dono do local também anuncia: o parceiro aponta para o cadastro de cliente dele.
        _local = new Parceiro { Nome = "Café Central", FusoHorario = "America/Manaus", Cliente = _host };
        _context.Parceiros.Add(_local);
        _playlist = new Playlist { Nome = "Grade" };
        _context.Playlists.Add(_playlist);
        _tela = new Tela { Parceiro = _local, Nome = "Balcão", Playlist = _playlist, DiretorioAtivo = true };
        _tela.DefinirCodigoDiretorio(Codigo);
        _context.Telas.Add(_tela);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ---------- Quem aparece ----------

    [Fact]
    public async Task Diretorio_SoQuemAceitouEEstaNaGradeDeHoje_ComOHostPrimeiro()
    {
        Aceitar(_host);
        var zeca = Anunciante("Zeca Pneus", aceitou: true);
        var academia = Anunciante("academia Forte", aceitou: true);
        var semAceite = Anunciante("Ótica Sem Aceite", aceitou: false);
        await Grade(Midia(zeca), Midia(academia), Midia(semAceite));

        var diretorio = await Consultar();

        diretorio.Estabelecimento.Should().Be("Café Central");
        diretorio.Anunciantes.Select(a => a.Nome).Should().Equal("Café Central", "academia Forte", "Zeca Pneus");
        diretorio.Anunciantes[0].Anfitriao.Should().BeTrue();
        diretorio.Anunciantes[0].Categoria.Should().Be("Cafeteria");
        diretorio.Anunciantes.Skip(1).Should().OnlyContain(a => !a.Anfitriao);
    }

    [Fact]
    public async Task Diretorio_ComContatoDaOperadoraTemOBotaoAnuncieComOLocal()
    {
        await Grade(Array.Empty<Midia>());

        var semContato = await Consultar();
        var comContato = await new GetDiretorioPublicoQueryHandler(_context, _relogio, new ConfiguracaoIndoorFake { WhatsAppAnuncie = "5592999990000" })
            .Handle(new GetDiretorioPublicoQuery(Codigo), CancellationToken.None);

        semContato.LinkAnuncie.Should().BeNull();
        comContato.LinkAnuncie.Should().Be("https://wa.me/5592999990000?text=" + Uri.EscapeDataString("Olá! Vi a TV no Café Central e quero saber como anunciar."));
        PaginaDiretorio.Renderizar(comContato, "n").Should().Contain("Quer anunciar aqui?").And.Contain("Clique aqui e fale com a gente")
            .And.Contain("href=\"https://wa.me/5592999990000?text=");
        PaginaDiretorio.Renderizar(semContato, "n").Should().NotContain("Quer anunciar aqui?");
    }

    [Fact]
    public async Task Diretorio_HostSemAceiteNaoAparece()
    {
        var loja = Anunciante("Loja", aceitou: true);
        await Grade(Midia(loja));

        var diretorio = await Consultar();

        diretorio.Estabelecimento.Should().Be("Café Central", "o título é o nome do parceiro, sem depender do aceite");
        diretorio.Anunciantes.Select(a => a.Nome).Should().Equal("Loja");
    }

    [Fact]
    public async Task Diretorio_ParceiroQueNaoAnunciaNaoTemCartaoDeAnfitriao()
    {
        Aceitar(_host);
        _local.ClienteId = null;
        _local.Cliente = null;
        var loja = Anunciante("Loja", aceitou: true);
        await Grade(Midia(loja));

        var diretorio = await Consultar();

        diretorio.Anunciantes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Nome = "Loja", Anfitriao = false });
    }

    [Fact]
    public async Task Diretorio_ForaDaGradeDeHojeNaoAparece()
    {
        var inativa = Anunciante("Inativa", aceitou: true);
        var vencida = Anunciante("Vencida", aceitou: true);
        var futura = Anunciante("Futura", aceitou: true);
        var soDomingo = Anunciante("Só Domingo", aceitou: true);
        var bloqueado = Anunciante("Bloqueado", aceitou: true);
        bloqueado.Bloquear("Inadimplência");
        var ok = Anunciante("Ok", aceitou: true);

        await Grade(
            (Midia(inativa, ativa: false), DiasSemana.Todos),
            (Midia(vencida, fim: Hoje.AddDays(-1)), DiasSemana.Todos),
            (Midia(futura, inicio: Hoje.AddDays(1)), DiasSemana.Todos),
            (Midia(soDomingo), DiasSemana.Domingo),
            (Midia(bloqueado), DiasSemana.Todos),
            (Midia(ok), DiasSemana.Segunda));

        (await Consultar()).Anunciantes.Select(a => a.Nome).Should().Equal("Ok");
    }

    [Fact]
    public async Task Diretorio_PlaylistInativaMostraSoOHost()
    {
        Aceitar(_host);
        await Grade(Midia(Anunciante("Loja", aceitou: true)));
        _playlist.Ativo = false;
        await _context.SaveChangesAsync();

        (await Consultar()).Anunciantes.Select(a => a.Nome).Should().Equal("Café Central");
    }

    [Fact]
    public async Task Diretorio_SoOsContatosDoPerfil_OsQrCodesDosAnunciosNaoEntram()
    {
        var loja = Anunciante("Loja", aceitou: true);
        loja.WhatsappPublico = "5592999990000";
        loja.InstagramPublico = "loja";
        loja.SitePublico = "https://loja.test/";
        var outroWhatsApp = Midia(loja);
        outroWhatsApp.DefinirQrCode(new QrCodeDaMidia(TipoQrCode.WhatsApp, "5592988880000", "Quero a promoção", null, null, null, null,
            PosicaoQrCode.InferiorDireito, TamanhoQrCode.Medio, "https://wa.me/5592988880000?text=Quero%20a%20promo%C3%A7%C3%A3o"));
        var pix = Midia(loja);
        pix.DefinirQrCode(new QrCodeDaMidia(TipoQrCode.Pix, "+5592999990000", null, null, "Loja", "Manaus", null,
            PosicaoQrCode.InferiorDireito, TamanhoQrCode.Medio, "00020126...6304ABCD"));
        await Grade(outroWhatsApp, pix);

        var contatos = (await Consultar()).Anunciantes.Single().Contatos;

        contatos.Should().Equal(
            new ContatoDiretorioDto(TipoQrCode.WhatsApp, "WhatsApp", "https://wa.me/5592999990000", "(92) 99999-0000"),
            new ContatoDiretorioDto(TipoQrCode.Instagram, "Instagram", "https://instagram.com/loja", "@loja"),
            new ContatoDiretorioDto(TipoQrCode.Link, "Site", "https://loja.test/", "loja.test"));
    }

    [Fact]
    public async Task Diretorio_SemContatosNoPerfilAparecemSoONomeEACategoria()
    {
        var loja = Anunciante("Loja", aceitou: true);
        loja.Categoria = "Ótica";
        var comQr = Midia(loja);
        comQr.DefinirQrCode(new QrCodeDaMidia(TipoQrCode.Link, "https://loja.test/", null, null, null, null, null,
            PosicaoQrCode.InferiorDireito, TamanhoQrCode.Medio, "https://loja.test/"));
        await Grade(comQr);

        var cartao = (await Consultar()).Anunciantes.Single();

        cartao.Categoria.Should().Be("Ótica");
        cartao.Contatos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("5592999990000", "(92) 99999-0000")]
    [InlineData("559233330000", "(92) 3333-0000")]
    [InlineData("14155550100", "+14155550100")]
    public void Diretorio_TelefoneLegivel(string digitos, string esperado) =>
        GetDiretorioPublicoQueryHandler.TelefoneLegivel(digitos).Should().Be(esperado);

    // ---------- 404 ----------

    [Theory]
    [InlineData("ZZZZZZZZZZ")]
    [InlineData("curto")]
    [InlineData("ABCDEFGH2'")]
    public async Task Diretorio_CodigoInexistenteOuInvalidoDa404(string codigo)
    {
        var acao = () => Consultar(codigo);
        await acao.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Diretorio_DesligadoOuTelaInativaDa404_EOCodigoIgnoraMaiusculas()
    {
        (await Consultar(" abcdefgh23 ")).Should().NotBeNull();

        _tela.DiretorioAtivo = false;
        await _context.SaveChangesAsync();
        var desligado = () => Consultar();
        await desligado.Should().ThrowAsync<NotFoundException>();

        _tela.DiretorioAtivo = true;
        _tela.Ativo = false;
        await _context.SaveChangesAsync();
        var inativa = () => Consultar();
        await inativa.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Perfil público (aceite do cliente) ----------

    [Fact]
    public async Task Perfil_DonoAceitaENormalizaOsContatos()
    {
        var clienteAtual = new ClienteAtualFake(_host.Id);

        await new AtualizarPerfilPublicoCommandHandler(_context, clienteAtual, _relogio).Handle(new AtualizarPerfilPublicoCommand
        {
            ExibirNoDiretorioPublico = true,
            Categoria = " Cafeteria ",
            DescricaoPublica = "Café coado na hora",
            WhatsappPublico = "(92) 99999-0000",
            InstagramPublico = "https://instagram.com/cafe.central/",
            SitePublico = "https://cafe.test",
        }, CancellationToken.None);

        var perfil = await new GetPerfilPublicoQueryHandler(_context, clienteAtual).Handle(new GetPerfilPublicoQuery(), CancellationToken.None);
        perfil.ExibirNoDiretorioPublico.Should().BeTrue();
        perfil.DiretorioAceitoEm.Should().Be(Agora);
        perfil.Categoria.Should().Be("Cafeteria");
        perfil.WhatsappPublico.Should().Be("5592999990000");
        perfil.InstagramPublico.Should().Be("cafe.central");
        perfil.SitePublico.Should().Be("https://cafe.test/");
        perfil.PodeEditar.Should().BeTrue();

        // Salvar de novo não muda a data do aceite; desligar apaga.
        _relogio.Agora = Agora.AddDays(1);
        await Salvar(clienteAtual, exibir: true);
        _host.DiretorioAceitoEm.Should().Be(Agora);
        await Salvar(clienteAtual, exibir: false);
        _host.ExibirNoDiretorioPublico.Should().BeFalse();
        _host.DiretorioAceitoEm.Should().BeNull();
    }

    [Fact]
    public async Task Perfil_SoODonoMudaOAceite()
    {
        var equipe = new ClienteAtualFake(_host.Id) { Papel = PapelCliente.Equipe };

        var acao = () => Salvar(equipe, exibir: true);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
        _host.ExibirNoDiretorioPublico.Should().BeFalse();
        (await new GetPerfilPublicoQueryHandler(_context, equipe).Handle(new GetPerfilPublicoQuery(), CancellationToken.None))
            .PodeEditar.Should().BeFalse();
    }

    [Fact]
    public async Task Perfil_ContatosInvalidosVoltamPorCampoSemGravar()
    {
        var acao = () => new AtualizarPerfilPublicoCommandHandler(_context, new ClienteAtualFake(_host.Id), _relogio)
            .Handle(new AtualizarPerfilPublicoCommand
            {
                ExibirNoDiretorioPublico = true,
                WhatsappPublico = "123",
                InstagramPublico = "não vale",
                SitePublico = "javascript:alert(1)",
            }, CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Keys.Should().BeEquivalentTo("WhatsappPublico", "InstagramPublico", "SitePublico");
        _host.ExibirNoDiretorioPublico.Should().BeFalse();
    }

    // ---------- Tela (operadora) ----------

    [Fact]
    public async Task Tela_LigarCriaOEnderecoDesligarMantemETrocarGeraOutro()
    {
        var configuracao = new ConfiguracaoIndoorFake();
        var id = await new CriarTelaCommandHandler(_context).Handle(
            new CriarTelaCommand { ParceiroId = _local.Id, Nome = "Salão" }, CancellationToken.None);
        (await _context.Telas.SingleAsync(t => t.Id == id)).CodigoDiretorio.Should().BeNull();

        await Atualizar(id, diretorio: true);
        var codigo = (await _context.Telas.SingleAsync(t => t.Id == id)).CodigoDiretorio;
        codigo.Should().MatchRegex("^[A-Z2-9]{10}$");

        await Atualizar(id, diretorio: false);
        (await _context.Telas.SingleAsync(t => t.Id == id)).CodigoDiretorio.Should().Be(codigo, "o QR impresso continua valendo");

        var novo = await new TrocarCodigoDiretorioCommandHandler(_context, configuracao)
            .Handle(new TrocarCodigoDiretorioCommand(id, 0), CancellationToken.None);
        novo.Codigo.Should().NotBe(codigo);
        novo.Url.Should().Be(new Uri($"https://tv.indoor.test/d/{novo.Codigo}"));

        var dto = await new GetTelaRedeByIdQueryHandler(_context, new IdentityNomesFake(), configuracao, _relogio)
            .Handle(new GetTelaRedeByIdQuery(id), CancellationToken.None);
        dto.CodigoDiretorio.Should().Be(novo.Codigo);
        dto.UrlDiretorio.Should().Be(novo.Url);
    }

    [Theory]
    [InlineData("https://tv.rede.test", "https://tv.rede.test/d/ABCDEFGH23")]
    [InlineData("https://rede.test/indoor", "https://rede.test/indoor/d/ABCDEFGH23")]
    [InlineData("https://rede.test/indoor/", "https://rede.test/indoor/d/ABCDEFGH23")]
    public void Endereco_RespeitaACaminhoDaUrlPublica(string urlPublica, string esperado)
    {
        var configuracao = new ConfiguracaoIndoorFake { UrlPublica = new Uri(urlPublica) };

        EnderecoDiretorio.Montar(configuracao, Codigo).Should().Be(new Uri(esperado));
        EnderecoDiretorio.Montar(configuracao, null).Should().BeNull();
        EnderecoDiretorio.Montar(new ConfiguracaoIndoorFake { UrlPublica = null }, Codigo).Should().BeNull();
    }

    // ---------- Página pública ----------

    [Fact]
    public void Pagina_EscapaOTextoEDescartaEnderecosPerigosos()
    {
        var html = PaginaDiretorio.Renderizar(new DiretorioPublicoDto
        {
            Estabelecimento = "Bar <b>do Zé</b>",
            Anunciantes =
            [
                new AnuncianteDiretorioDto
                {
                    Nome = "<script>alert(1)</script>",
                    Categoria = "\"><img src=x>",
                    Contatos =
                    [
                        new(TipoQrCode.Link, "Site", "javascript:alert(1)"),
                        new(TipoQrCode.WhatsApp, "WhatsApp", "https://wa.me/5592999990000?text=a\"b"),
                        new(TipoQrCode.Pix, "Copiar Pix", "000201\"><x"),
                        new(TipoQrCode.Telefone, "Ligar", "tel:+559233330000"),
                    ],
                },
            ],
        }, "n0nce");

        html.Should().NotContain("<script>alert").And.NotContain("<img").And.NotContain("<b>do");
        html.Should().NotContain("javascript:");
        html.Should().Contain("href=\"https://wa.me/5592999990000?text=a&quot;b\"");
        html.Should().Contain("data-pix=\"000201&quot;&gt;&lt;x\"");
        html.Should().Contain("href=\"tel:+559233330000\"");
        html.Should().Contain("<style nonce=\"n0nce\">").And.Contain("<script nonce=\"n0nce\">");
        html.Should().Contain("noindex");
        PaginaDiretorio.Politica("n0nce").Should().Contain("script-src 'nonce-n0nce'").And.Contain("frame-ancestors 'none'");
    }

    [Fact]
    public void Pagina_MostraODetalheEEscondeDescricaoIgualACategoria()
    {
        var html = PaginaDiretorio.Renderizar(new DiretorioPublicoDto
        {
            Anunciantes =
            [
                new AnuncianteDiretorioDto
                {
                    Nome = "Academia Shape", Categoria = "Academia", Descricao = "academia",
                    Contatos = [new(TipoQrCode.WhatsApp, "WhatsApp", "https://wa.me/5592999990000", "(92) 99999-0000")],
                },
            ],
        }, "n");

        html.Should().Contain("<span class=\"detalhe\">(92) 99999-0000</span>");
        html.Should().NotContain("class=\"descricao\"");
        html.Should().Contain("Fale direto com o anunciante");
    }

    [Fact]
    public void Pagina_SemAnunciantesMostraOAviso()
    {
        PaginaDiretorio.Renderizar(new DiretorioPublicoDto(), "n").Should().Contain("Nenhum anunciante publicou");
    }

    [Theory]
    [InlineData("Café Central", "CC")]
    [InlineData("academia", "AC")]
    [InlineData("Zé da Esquina Bar", "ZB")]
    [InlineData("- !", "?")]
    public void Pagina_Iniciais(string nome, string esperado) => PaginaDiretorio.Iniciais(nome).Should().Be(esperado);

    // ---------- Contagem de acessos e toques (ADR-027) ----------

    [Fact]
    public async Task Toques_SoContaOQueAPaginaOferece()
    {
        var loja = Anunciante("Loja", aceitou: true);
        loja.WhatsappPublico = "5592999990000";
        var fora = Anunciante("Fora da Grade", aceitou: true);
        fora.WhatsappPublico = "5592888880000";
        await Grade(Midia(loja));
        var diretorio = await Consultar();

        (await Interagir(diretorio, EventoDiretorio.Acesso)).Should().BeTrue();
        (await Interagir(diretorio, EventoDiretorio.Contato, loja.Id, TipoQrCode.WhatsApp)).Should().BeTrue();
        (await Interagir(diretorio, EventoDiretorio.Contato, loja.Id, TipoQrCode.Instagram)).Should().BeFalse("a loja não publicou Instagram");
        (await Interagir(diretorio, EventoDiretorio.Contato, fora.Id, TipoQrCode.WhatsApp)).Should().BeFalse("não está no diretório desta TV");
        (await Interagir(diretorio, EventoDiretorio.Contato, loja.Id, null)).Should().BeFalse();
        (await Interagir(diretorio, EventoDiretorio.Anuncie)).Should().BeFalse("sem o WhatsApp da operadora não há o botão");
        (await Interagir(diretorio with { LinkAnuncie = "https://wa.me/5592999990000" }, EventoDiretorio.Anuncie)).Should().BeTrue();

        var gravados = await _context.InteracoesDiretorio.OrderBy(i => i.Id).ToListAsync();
        gravados.Select(i => i.Evento).Should().Equal(EventoDiretorio.Acesso, EventoDiretorio.Contato, EventoDiretorio.Anuncie);
        gravados.Should().OnlyContain(i => i.TelaId == _tela.Id && i.ParceiroId == _local.Id && i.Momento == Agora);
        gravados[1].ClienteId.Should().Be(loja.Id);
        gravados[1].Contato.Should().Be(TipoQrCode.WhatsApp);
        gravados[0].ClienteId.Should().BeNull();
        gravados.Should().OnlyContain(i => i.Visitante.Length == 16, "hash diário, sem o IP");
    }

    [Fact]
    public async Task UsoDoDiretorio_ContaAcessosPessoasEToquesPorAnunciante()
    {
        var loja = Anunciante("Loja", aceitou: true);
        loja.WhatsappPublico = "5592999990000";
        loja.InstagramPublico = "loja";
        await Grade(Midia(loja));
        var diretorio = await Consultar() with { LinkAnuncie = "https://wa.me/5592999990000" };

        await Interagir(diretorio, EventoDiretorio.Acesso, ip: "1.1.1.1");
        await Interagir(diretorio, EventoDiretorio.Acesso, ip: "1.1.1.1");
        await Interagir(diretorio, EventoDiretorio.Acesso, ip: "2.2.2.2");
        await Interagir(diretorio, EventoDiretorio.Contato, loja.Id, TipoQrCode.WhatsApp);
        await Interagir(diretorio, EventoDiretorio.Contato, loja.Id, TipoQrCode.Instagram);
        await Interagir(diretorio, EventoDiretorio.Anuncie);
        _context.InteracoesDiretorio.Add(new InteracaoDiretorio
        {
            TelaId = _tela.Id, ParceiroId = _local.Id, Evento = EventoDiretorio.Acesso, Momento = Agora.AddDays(-40), Visitante = new string('a', 16),
        });
        await _context.SaveChangesAsync();

        var uso = await new GetUsoDiretorioTelaQueryHandler(_context, _relogio).Handle(new GetUsoDiretorioTelaQuery(_tela.Id), CancellationToken.None);

        uso.Should().BeEquivalentTo(new UsoDiretorioDto(30, 3, 2, 2, 1, [new ToquesAnuncianteDto(loja.Id, "Loja", 2)]),
            "o acesso de 40 dias atrás fica fora; duas visitas do mesmo celular no dia são uma pessoa");

        var relatorio = await Atlas.Application.Business.Veiculacao.RelatorioVeiculacao.MontarAsync(_context, loja.Id, null, null, Agora, CancellationToken.None);
        relatorio.ToquesDiretorio.Should().Be(2);
        relatorio.PessoasDiretorio.Should().Be(1);
        relatorio.ToquesPorContato.Select(c => c.Rotulo).Should().BeEquivalentTo("WhatsApp", "Instagram");
        relatorio.PorDia.Single().ToquesDiretorio.Should().Be(2);
    }

    [Fact]
    public async Task UsoDoDiretorio_TelaInexistenteDa404()
    {
        var acao = () => new GetUsoDiretorioTelaQueryHandler(_context, _relogio).Handle(new GetUsoDiretorioTelaQuery(999_999), CancellationToken.None);

        await acao.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Pagina_MarcaOsToquesELiberaOEnvioSoParaOProprioSite()
    {
        var loja = Anunciante("Loja", aceitou: true);
        loja.WhatsappPublico = "5592999990000";
        await Grade(Midia(loja));
        var diretorio = await Consultar() with { LinkAnuncie = "https://wa.me/5592999990000" };

        var html = PaginaDiretorio.Renderizar(diretorio, "n");

        html.Should().Contain($"data-toque=\"contato\" data-cliente=\"{loja.Id}\" data-tipo=\"WhatsApp\"")
            .And.Contain("data-toque=\"anuncie\"")
            .And.Contain("navigator.sendBeacon");
        PaginaDiretorio.Politica("n").Should().Contain("connect-src 'self'");

        // Os ids internos ficam fora do JSON público.
        var json = System.Text.Json.JsonSerializer.Serialize(diretorio);
        json.Should().NotContain("TelaId").And.NotContain("ParceiroId").And.NotContain("ClienteId");
    }

    // ---------- Apoio ----------

    private Task<bool> Interagir(DiretorioPublicoDto diretorio, EventoDiretorio evento, long? clienteId = null, TipoQrCode? contato = null, string ip = "10.0.0.1") =>
        new RegistrarInteracaoDiretorioCommandHandler(_context, _relogio)
            .Handle(new RegistrarInteracaoDiretorioCommand(diretorio, evento, clienteId, contato, ip, "Mozilla/5.0"), CancellationToken.None);

    private Task<DiretorioPublicoDto> Consultar(string codigo = Codigo) =>
        new GetDiretorioPublicoQueryHandler(_context, _relogio).Handle(new GetDiretorioPublicoQuery(codigo), CancellationToken.None);

    private Task<uint> Salvar(ClienteAtualFake clienteAtual, bool exibir) =>
        new AtualizarPerfilPublicoCommandHandler(_context, clienteAtual, _relogio)
            .Handle(new AtualizarPerfilPublicoCommand { ExibirNoDiretorioPublico = exibir }, CancellationToken.None);

    private Task<uint> Atualizar(long id, bool diretorio) =>
        new AtualizarTelaCommandHandler(_context).Handle(
            new AtualizarTelaCommand { Id = id, ParceiroId = _local.Id, Nome = "Salão", DiretorioAtivo = diretorio }, CancellationToken.None);

    private void Aceitar(Cliente cliente)
    {
        cliente.DefinirExibicaoNoDiretorio(true, Agora);
        _context.SaveChanges();
    }

    private Cliente Anunciante(string nome, bool aceitou)
    {
        var cliente = new Cliente { Nome = nome, Email = $"{Guid.NewGuid():N}@teste.local" };
        if (aceitou)
        {
            cliente.DefinirExibicaoNoDiretorio(true, Agora);
        }

        _context.Clientes.Add(cliente);
        _context.SaveChanges();
        return cliente;
    }

    private static Midia Midia(Cliente cliente, DateOnly? inicio = null, DateOnly? fim = null, bool ativa = true) => new()
    {
        ClienteId = cliente.Id,
        Titulo = "Anúncio",
        Tipo = TipoMidia.Imagem,
        ChaveArmazenamento = ChavesArmazenamento.Gerar(cliente.Id, TiposArquivoMidia.Png),
        TipoConteudo = TiposArquivoMidia.Png,
        TamanhoBytes = 1000,
        Hash = new string('c', 64),
        InicioExibicao = inicio,
        FimExibicao = fim,
        Ativo = ativa,
    };

    private Task Grade(params Midia[] midias) => Grade(midias.Select(m => (m, DiasSemana.Todos)).ToArray());

    private async Task Grade(params (Midia Midia, DiasSemana Dias)[] itens)
    {
        _context.Midias.AddRange(itens.Select(i => i.Midia));
        await _context.SaveChangesAsync();
        _playlist.DefinirItens(itens.Select(i => new ItemPlaylistInformado(null, i.Midia.Id, i.Dias)).ToList());
        await _context.SaveChangesAsync();
    }
}
