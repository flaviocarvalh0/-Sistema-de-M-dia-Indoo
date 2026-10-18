using Atlas.Application.Business.Indoor.Midias.Commands.AtualizarMidia;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>QR code do anúncio: cada tipo com os seus campos, o Pix copia e cola e a gravação.</summary>
public class QrCodeMidiaTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose() => _context.Dispose();

    private static AtualizarMidiaCommand Qr(
        TipoQrCode? tipo, string? destino = null, string? mensagem = null, decimal? valor = null,
        string? nome = null, string? cidade = null, string? legenda = null) => new()
        {
            QrTipo = tipo, QrDestino = destino, QrMensagem = mensagem, QrValor = valor,
            QrNomeRecebedor = nome, QrCidadeRecebedor = cidade, QrLegenda = legenda,
        };

    [Fact]
    public void SemTipo_SemQrCode() => MontadorQrCode.Montar(Qr(null, "https://x.test")).Should().BeNull();

    [Theory]
    [InlineData(TipoQrCode.Link, "https://padaria.test/cardapio?mesa=1", "https://padaria.test/cardapio?mesa=1")]
    [InlineData(TipoQrCode.WhatsApp, "(92) 99999-0000", "https://wa.me/5592999990000")]
    [InlineData(TipoQrCode.WhatsApp, "+351 912 345 678", "https://wa.me/351912345678")]
    [InlineData(TipoQrCode.Instagram, "@padaria.centro", "https://instagram.com/padaria.centro")]
    [InlineData(TipoQrCode.Instagram, "https://www.instagram.com/padaria_centro/", "https://instagram.com/padaria_centro")]
    [InlineData(TipoQrCode.Telefone, "92 3333-0000", "tel:+559233330000")]
    public void Monta_OConteudoDeCadaTipo(TipoQrCode tipo, string destino, string conteudo)
    {
        var qr = MontadorQrCode.Montar(Qr(tipo, destino))!;

        qr.Conteudo.Should().Be(conteudo);
        qr.Posicao.Should().Be(PosicaoQrCode.InferiorDireito);
        qr.Tamanho.Should().Be(TamanhoQrCode.Medio);
    }

    [Fact]
    public void WhatsApp_ComMensagemInicial()
    {
        MontadorQrCode.Montar(Qr(TipoQrCode.WhatsApp, "92999990000", "Quero o combo do dia!"))!
            .Conteudo.Should().Be("https://wa.me/5592999990000?text=Quero%20o%20combo%20do%20dia%21");
    }

    [Theory]
    [InlineData(TipoQrCode.Link, "padaria.test", "QrDestino")]
    [InlineData(TipoQrCode.Link, "ftp://padaria.test", "QrDestino")]
    [InlineData(TipoQrCode.Link, "", "QrDestino")]
    [InlineData(TipoQrCode.WhatsApp, "9999", "QrDestino")]
    [InlineData(TipoQrCode.Instagram, "padaria centro", "QrDestino")]
    [InlineData(TipoQrCode.Telefone, "abc", "QrDestino")]
    public void Recusa_OQueNaoServe(TipoQrCode tipo, string destino, string campo)
    {
        var acao = () => MontadorQrCode.Montar(Qr(tipo, destino));

        acao.Should().Throw<ValidationException>().Which.Errors.Should().ContainKey(campo);
    }

    [Fact]
    public void Legenda_ComLimite()
    {
        var acao = () => MontadorQrCode.Montar(Qr(TipoQrCode.Link, "https://x.test", legenda: new string('a', 41)));
        acao.Should().Throw<ValidationException>().Which.Errors.Should().ContainKey("QrLegenda");
    }

    // ---------- Pix ----------

    [Fact]
    public void Pix_ExemploDoManualDoBancoCentral()
    {
        // Exemplo publicado no Manual de Padrões para Iniciação do Pix (BR Code estático).
        PixCopiaECola.Montar("123e4567-e12b-12d1-a456-426655440000", "Fulano de Tal", "BRASILIA", null)
            .Should().Be("00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-4266554400005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***63041D3D");
    }

    [Fact]
    public void Pix_ComValorENomeComAcento()
    {
        var qr = MontadorQrCode.Montar(Qr(TipoQrCode.Pix, "fulano@padaria.test", valor: 12.5m, nome: "Padaria São João", cidade: "Manaus"))!;

        qr.Destino.Should().Be("fulano@padaria.test");
        qr.NomeRecebedor.Should().Be("Padaria Sao Joao");
        qr.Conteudo.Should().Contain("540512.50").And.Contain("5916Padaria Sao Joao").And.Contain("6006Manaus");
        // O CRC fecha o próprio texto.
        var semCrc = qr.Conteudo[..^4];
        PixCopiaECola.Crc16(semCrc).ToString("X4").Should().Be(qr.Conteudo[^4..]);
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]          // CPF válido
    [InlineData("11.222.333/0001-81", "11222333000181")]    // CNPJ válido
    [InlineData("+55 (92) 99999-0000", "+5592999990000")]
    [InlineData("92 99999-0000", "+5592999990000")]
    [InlineData("Fulano@Padaria.TEST", "fulano@padaria.test")]
    [InlineData("123E4567-E12B-12D1-A456-426655440000", "123e4567-e12b-12d1-a456-426655440000")]
    [InlineData("123.456.789-00", null)]                    // CPF inválido (e não é telefone: 11 dígitos de CPF)
    [InlineData("chave qualquer", null)]
    public void Pix_NormalizaAChave(string chave, string? esperada)
    {
        PixCopiaECola.NormalizarChave(chave).Should().Be(esperada);
    }

    [Fact]
    public void Pix_ExigeRecebedorECidade()
    {
        var acao = () => MontadorQrCode.Montar(Qr(TipoQrCode.Pix, "52998224725"));

        acao.Should().Throw<ValidationException>().Which.Errors.Keys
            .Should().BeEquivalentTo("QrNomeRecebedor", "QrCidadeRecebedor");
    }

    // ---------- Gravação ----------

    [Fact]
    public async Task Atualizar_GravaETiraOQrCode()
    {
        var cliente = new Cliente { Nome = "Academia", Email = "a@teste.local" };
        _context.Clientes.Add(cliente);
        var midia = new Midia
        {
            Cliente = cliente, Titulo = "Promo", Tipo = TipoMidia.Imagem, ChaveArmazenamento = "c", TipoConteudo = TiposArquivoMidia.Png,
            TamanhoBytes = 1, Hash = new string('a', 64),
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();
        var handler = new AtualizarMidiaCommandHandler(_context, new ClienteAtualFake(cliente.Id));

        await handler.Handle(Qr(TipoQrCode.WhatsApp, "92999990000", legenda: "Peça já") with
        {
            Id = midia.Id, Titulo = "Promo", DuracaoSegundos = 15, QrPosicao = PosicaoQrCode.SuperiorEsquerdo, QrTamanho = TamanhoQrCode.Grande,
        }, CancellationToken.None);

        var gravada = await _context.Midias.SingleAsync();
        gravada.QrTipo.Should().Be(TipoQrCode.WhatsApp);
        gravada.QrConteudo.Should().Be("https://wa.me/5592999990000");
        gravada.QrLegenda.Should().Be("Peça já");
        gravada.QrPosicao.Should().Be(PosicaoQrCode.SuperiorEsquerdo);
        gravada.QrTamanho.Should().Be(TamanhoQrCode.Grande);

        await handler.Handle(Qr(null) with { Id = midia.Id, Titulo = "Promo", DuracaoSegundos = 15 }, CancellationToken.None);
        (await _context.Midias.SingleAsync()).QrConteudo.Should().BeNull();
    }

    [Fact]
    public async Task Atualizar_QrInvalidoNaoGravaNada()
    {
        var cliente = new Cliente { Nome = "Academia", Email = "a@teste.local" };
        _context.Clientes.Add(cliente);
        var midia = new Midia
        {
            Cliente = cliente, Titulo = "Promo", Tipo = TipoMidia.Imagem, ChaveArmazenamento = "c", TipoConteudo = TiposArquivoMidia.Png,
            TamanhoBytes = 1, Hash = new string('a', 64),
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();

        var acao = () => new AtualizarMidiaCommandHandler(_context, new ClienteAtualFake(cliente.Id))
            .Handle(Qr(TipoQrCode.Link, "sem-https") with { Id = midia.Id, Titulo = "Outro título", DuracaoSegundos = 15 }, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>();
        (await _context.Midias.AsNoTracking().SingleAsync()).Titulo.Should().Be("Promo");
    }
}
