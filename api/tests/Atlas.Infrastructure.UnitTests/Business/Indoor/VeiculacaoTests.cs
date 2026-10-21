using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;
using Atlas.Application.Business.Veiculacao;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>QR rastreado e relatório de veiculação (ADR-023).</summary>
public class VeiculacaoTests : IDisposable
{
    // Segunda-feira, 12h em Manaus (16h UTC).
    private static readonly DateTimeOffset Agora = new(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly RelogioFixo _relogio = new(Agora);
    private readonly Cliente _cliente;
    private readonly Cliente _outro;
    private readonly Tela _tela;

    public VeiculacaoTests()
    {
        _cliente = new Cliente { Nome = "Padaria", Email = "p@teste.local", FusoHorario = "America/Manaus" };
        _outro = new Cliente { Nome = "Outro", Email = "o@teste.local" };
        _context.Clientes.AddRange(_cliente, _outro);
        _tela = new Tela { Parceiro = new Parceiro { Nome = "Academia Centro", FusoHorario = "America/Manaus" }, Nome = "Recepção" };
        _context.Telas.Add(_tela);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ---------- Código do QR rastreado ----------

    [Fact]
    public async Task Midia_ComWhatsAppGanhaCodigo_MantemNaEdicao_PerdeNoPix()
    {
        var midia = await MidiaAsync(_cliente, "Promo");

        await Atualizar(midia, TipoQrCode.WhatsApp, "92999990000");
        var codigo = midia.CodigoQr;
        codigo.Should().MatchRegex("^[A-Z2-9]{8}$");

        await Atualizar(midia, TipoQrCode.Instagram, "@padaria");
        midia.CodigoQr.Should().Be(codigo, "o código não muda a cada edição");

        await Atualizar(midia, TipoQrCode.Pix, "chave@pix.test", nome: "Padaria", cidade: "Manaus");
        midia.CodigoQr.Should().BeNull("o Pix copia e cola precisa ir direto no QR");

        await Atualizar(midia, null, null);
        midia.CodigoQr.Should().BeNull();
    }

    [Fact]
    public void LinkQr_MontaComOEnderecoPublico()
    {
        LinkQr.Montar(new ConfiguracaoIndoorFake(), "ABCD2345")!.AbsoluteUri.Should().Be("https://tv.indoor.test/q/ABCD2345");
        LinkQr.Montar(new ConfiguracaoIndoorFake { UrlPublica = new Uri("https://api.test/base/") }, "ABCD2345")!.AbsoluteUri
            .Should().Be("https://api.test/base/q/ABCD2345");
        LinkQr.Montar(new ConfiguracaoIndoorFake { UrlPublica = null }, "ABCD2345").Should().BeNull();
        LinkQr.Montar(new ConfiguracaoIndoorFake(), null).Should().BeNull();
    }

    // ---------- Leitura ----------

    [Fact]
    public async Task Leitura_ContaERedirecionaSemGuardarIp()
    {
        var midia = await MidiaAsync(_cliente, "Promo");
        await Atualizar(midia, TipoQrCode.WhatsApp, "92999990000");

        var destino = await Ler(midia.CodigoQr!.ToLowerInvariant(), "200.1.2.3");

        destino!.AbsoluteUri.Should().Be("https://wa.me/5592999990000");
        var leitura = await _context.LeiturasQr.SingleAsync();
        leitura.Should().BeEquivalentTo(new { MidiaId = midia.Id, ClienteId = _cliente.Id, Momento = Agora });
        leitura.Visitante.Should().MatchRegex("^[0-9a-f]{16}$").And.NotContain("200");
    }

    [Fact]
    public async Task Leitura_CodigoInexistenteDa404()
    {
        (await Ler("ZZZZ9999", "1.1.1.1")).Should().BeNull();
        (await Ler("curto", "1.1.1.1")).Should().BeNull();
        _context.LeiturasQr.Should().BeEmpty();
    }

    [Fact]
    public void Visitante_MesmaPessoaNoMesmoDiaIgual_NoOutroDiaDiferente()
    {
        var dia = new DateOnly(2026, 10, 12);
        var a = RegistrarLeituraQrCommandHandler.Visitante("1.1.1.1", "Android", dia);

        RegistrarLeituraQrCommandHandler.Visitante("1.1.1.1", "Android", dia).Should().Be(a);
        RegistrarLeituraQrCommandHandler.Visitante("1.1.1.1", "Android", dia.AddDays(1)).Should().NotBe(a);
        RegistrarLeituraQrCommandHandler.Visitante("2.2.2.2", "Android", dia).Should().NotBe(a);
    }

    // ---------- Relatório ----------

    [Fact]
    public async Task Relatorio_SomaPorTelaMidiaDiaEHoraLocalESoDoCliente()
    {
        var promo = await MidiaAsync(_cliente, "Promo");
        var combo = await MidiaAsync(_cliente, "Combo");
        var deOutro = await MidiaAsync(_outro, "De outro");
        await Atualizar(promo, TipoQrCode.WhatsApp, "92999990000");

        // 7h e 7h30 em Manaus (11h e 11h30 UTC) de hoje; 20h de ontem (00h UTC de hoje).
        Exibicao(promo, Agora.AddHours(-5), 15_000);
        Exibicao(promo, Agora.AddHours(-4.5), 15_000, concluida: false, semConexao: true);
        Exibicao(combo, Agora.AddHours(-16), 10_000);
        Exibicao(deOutro, Agora.AddHours(-5), 15_000);
        await _context.SaveChangesAsync();

        await Ler(promo.CodigoQr!, "1.1.1.1");
        await Ler(promo.CodigoQr!, "1.1.1.1");
        await Ler(promo.CodigoQr!, "2.2.2.2");

        var r = await Relatorio(_cliente.Id);

        r.Should().BeEquivalentTo(new
        {
            ClienteNome = "Padaria",
            De = new DateOnly(2026, 10, 1),
            Ate = new DateOnly(2026, 10, 12),
            Exibicoes = 3,
            Concluidas = 2,
            Interrompidas = 1,
            SemConexao = 1,
            SegundosNaTela = 40L,
            LeiturasQr = 3,
            LeitoresUnicosQr = 2,
            MedeTelaLigada = false,
        });
        r.PorTela.Should().ContainSingle().Which.Should().Be(new VeiculacaoPorTelaDto(_tela.Id, "Recepção", "Academia Centro", 3, 40));
        r.PorMidia.Select(m => (m.Titulo, m.Exibicoes, m.LeiturasQr, m.QrRastreado))
            .Should().Equal(("Promo", 2, 3, true), ("Combo", 1, 0, false));
        r.PorHora[7].Should().Be(2, "7h e 7h30 no fuso da academia");
        r.PorHora[20].Should().Be(1);
        r.PorDia.Should().Equal(
            new VeiculacaoPorDiaDto(new DateOnly(2026, 10, 11), 1, 0, 0),
            new VeiculacaoPorDiaDto(new DateOnly(2026, 10, 12), 2, 3, 0));
    }

    [Fact]
    public async Task Relatorio_PeriodoFiltraEValida()
    {
        var promo = await MidiaAsync(_cliente, "Promo");
        Exibicao(promo, Agora.AddDays(-20), 1000);
        Exibicao(promo, Agora, 1000);
        await _context.SaveChangesAsync();

        (await Relatorio(_cliente.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))).Exibicoes.Should().Be(1);
        (await Relatorio(_cliente.Id)).Exibicoes.Should().Be(1, "padrão: do dia 1 do mês até hoje");

        var validador = new GetRelatorioVeiculacaoClienteQueryValidator();
        (await validador.ValidateAsync(new GetRelatorioVeiculacaoClienteQuery(1, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1)))).IsValid.Should().BeFalse();
        (await validador.ValidateAsync(new GetRelatorioVeiculacaoClienteQuery(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Relatorio_DoClienteLogadoUsaOVinculo()
    {
        var promo = await MidiaAsync(_cliente, "Promo");
        Exibicao(promo, Agora, 1000);
        await _context.SaveChangesAsync();

        var meu = await new GetMeuRelatorioVeiculacaoQueryHandler(_context, new ClienteAtualFake(_outro.Id), _relogio)
            .Handle(new GetMeuRelatorioVeiculacaoQuery(), CancellationToken.None);

        meu.ClienteNome.Should().Be("Outro");
        meu.Exibicoes.Should().Be(0, "a exibição é de outro cliente");
    }

    private Task<RelatorioVeiculacaoDto> Relatorio(long clienteId, DateOnly? de = null, DateOnly? ate = null) =>
        new GetRelatorioVeiculacaoClienteQueryHandler(_context, _relogio)
            .Handle(new GetRelatorioVeiculacaoClienteQuery(clienteId, de, ate), CancellationToken.None);

    private void Exibicao(Midia midia, DateTimeOffset inicio, int duracaoMs, bool concluida = true, bool semConexao = false) =>
        _context.RegistrosExibicao.Add(new RegistroExibicao
        {
            TelaId = _tela.Id, ParceiroId = _tela.ParceiroId, MidiaId = midia.Id, ClienteId = midia.ClienteId,
            IdLocal = Guid.NewGuid(), Inicio = inicio, DuracaoMs = duracaoMs, Concluida = concluida, SemConexao = semConexao,
            RecebidoEm = Agora,
        });

    private Task<Uri?> Ler(string codigo, string ip) =>
        new RegistrarLeituraQrCommandHandler(_context, _relogio).Handle(new RegistrarLeituraQrCommand(codigo, ip, "Android"), CancellationToken.None);

    private async Task Atualizar(Midia midia, TipoQrCode? tipo, string? destino, string? nome = null, string? cidade = null)
    {
        await new AtualizarMidiaCommandHandler(_context, new ClienteAtualFake(midia.ClienteId)).Handle(new AtualizarMidiaCommand
        {
            Id = midia.Id, Titulo = midia.Titulo, DuracaoSegundos = 15,
            QrTipo = tipo, QrDestino = destino, QrNomeRecebedor = nome, QrCidadeRecebedor = cidade,
            Version = 0,
        }, CancellationToken.None);
    }

    private async Task<Midia> MidiaAsync(Cliente cliente, string titulo)
    {
        var midia = new Midia
        {
            ClienteId = cliente.Id,
            Titulo = titulo,
            Tipo = TipoMidia.Imagem,
            ChaveArmazenamento = ChavesArmazenamento.Gerar(cliente.Id, TiposArquivoMidia.Png),
            TipoConteudo = TiposArquivoMidia.Png,
            TamanhoBytes = 1000,
            Hash = new string('a', 64),
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();
        return midia;
    }
}
