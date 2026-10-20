using System.Net;
using System.Text;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Commands.CriarMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EnvioMidia;
using Atlas.Application.Business.Indoor.Midias.Dtos;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Infrastructure.Business.Armazenamento;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Storage que lista o bucket, como o R2: o teste diz quando cada arquivo foi gravado.</summary>
internal sealed class ArmazenamentoListavelFake : ArmazenamentoFake, IListagemArmazenamento
{
    public Dictionary<string, DateTimeOffset> GravadosEm { get; } = [];

    public void Gravar(string chave, int tamanho, DateTimeOffset em)
    {
        Arquivos[chave] = new byte[tamanho];
        GravadosEm[chave] = em;
    }

    public async IAsyncEnumerable<ObjetoListado> ListarAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var (chave, dados) in Arquivos.ToList())
        {
            await Task.Yield();
            yield return new ObjetoListado(chave, dados.Length, GravadosEm.GetValueOrDefault(chave, DateTimeOffset.UnixEpoch));
        }
    }
}

/// <summary>O teto de 10 GB da rede (ADR-025): reservas, recebimento, inventário e alertas.</summary>
public class TetoArmazenamentoTests : IDisposable
{
    private const long Mb = 1024 * 1024;
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly RelogioFixo _relogio = new(Agora);
    private readonly ConfiguracaoIndoorFake _configuracao = new() { LimiteArmazenamentoBytes = 100 * Mb };
    private readonly ArmazenamentoListavelFake _armazenamento = new();
    private readonly EmailsFake _emails = new();
    private readonly long _clienteId;

    public TetoArmazenamentoTests()
    {
        var cliente = new Cliente { Nome = "Academia", Email = "a@teste.local", CotaArmazenamentoMb = 1024 };
        _context.Clientes.Add(cliente);
        _context.SaveChanges();
        _clienteId = cliente.Id;
    }

    public void Dispose() => _context.Dispose();

    private TetoArmazenamento Teto() => new(_context, _configuracao, _relogio);

    // ---------- Medição e reserva ----------

    [Fact]
    public async Task Medir_DeveSomarMidiasPendentesApksNoticiasEReservasAbertas()
    {
        Midia(60 * Mb);
        _context.VersoesApp.Add(new VersaoApp { Codigo = 1, Nome = "1.0", ChaveArmazenamento = "app/v1.apk", TamanhoBytes = 10 * Mb, Hash = new string('1', 64) });
        _context.Noticias.Add(new Noticia { FonteNoticiaId = 1, Identificador = "n", Titulo = "T", PublicadaEm = Agora, ColetadaEm = Agora, ChaveImagem = "noticias/1/a.jpg", TamanhoImagemBytes = 1 * Mb });
        Reservas.Criar(_context, "clientes/1/midias/a.mp4", 5 * Mb, Agora);
        Reservas.Criar(_context, "clientes/1/midias/velha.mp4", 50 * Mb, Agora - TimeSpan.FromHours(3)); // vencida
        await _context.SaveChangesAsync();

        var uso = await Teto().MedirAsync(CancellationToken.None);

        uso.ArquivosBytes.Should().Be(71 * Mb);
        uso.ReservadoBytes.Should().Be(5 * Mb);
        uso.LimiteBytes.Should().Be(100 * Mb);
        uso.Percentual.Should().Be(76);
        uso.LivreBytes.Should().Be(24 * Mb);
    }

    [Fact]
    public async Task Reservar_QueNaoCabeDeveSerRecusadoSemCriarReserva()
    {
        Midia(90 * Mb);
        await _context.SaveChangesAsync();

        var acao = () => Teto().ReservarAsync("clientes/1/midias/b.mp4", 11 * Mb, "TamanhoBytes", CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["TamanhoBytes"].Single()
            .Should().Contain("armazenamento da rede está cheio");
        (await _context.ReservasEnvio.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Reservar_DeveContarOsEnviosEmAndamento()
    {
        await Teto().ReservarAsync("clientes/1/midias/a.mp4", 60 * Mb, "TamanhoBytes", CancellationToken.None);

        var segundo = () => Teto().ReservarAsync("clientes/1/midias/b.mp4", 60 * Mb, "TamanhoBytes", CancellationToken.None);

        await segundo.Should().ThrowAsync<ValidationException>("a primeira reserva ainda ocupa o espaço");
    }

    [Fact]
    public async Task Reservar_DeveLiberarReservasVencidas()
    {
        await Teto().ReservarAsync("clientes/1/midias/a.mp4", 60 * Mb, "TamanhoBytes", CancellationToken.None);
        _relogio.Agora = Agora + TetoArmazenamento.ValidadeReserva + TimeSpan.FromMinutes(1);

        await Teto().ReservarAsync("clientes/1/midias/b.mp4", 60 * Mb, "TamanhoBytes", CancellationToken.None);

        (await _context.ReservasEnvio.Select(r => r.Chave).ToListAsync()).Should().Equal("clientes/1/midias/b.mp4");
    }

    [Fact]
    public async Task Medir_BucketMaiorQueOBancoDeveContarOPiorCaso()
    {
        Midia(10 * Mb);
        _context.InventariosArmazenamento.Add(new InventarioArmazenamento { Id = 1, VerificadoEm = Agora.AddMinutes(-30), BytesNoBucket = 95 * Mb, Objetos = 3 });
        await _context.SaveChangesAsync();

        var acao = () => Teto().ReservarAsync("clientes/1/midias/b.mp4", 10 * Mb, "TamanhoBytes", CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>("o bucket mediu 95 MB, mesmo que o banco diga 10");
    }

    [Fact]
    public async Task Medir_InventarioAntigoNaoDeveContar()
    {
        _context.InventariosArmazenamento.Add(new InventarioArmazenamento { Id = 1, VerificadoEm = Agora.AddHours(-4), BytesNoBucket = 95 * Mb });
        await _context.SaveChangesAsync();

        var uso = await Teto().MedirAsync(CancellationToken.None);

        uso.BucketBytes.Should().BeNull();
        uso.InventarioEm.Should().Be(Agora.AddHours(-4));
    }

    [Fact]
    public async Task ConteudoAutomatico_SoEntraAte90PorCento()
    {
        Midia(85 * Mb);
        await _context.SaveChangesAsync();

        (await Teto().CabeConteudoAutomaticoAsync(4 * Mb, CancellationToken.None)).Should().BeTrue();
        (await Teto().CabeConteudoAutomaticoAsync(6 * Mb, CancellationToken.None)).Should().BeFalse();
    }

    // ---------- Envio e recebimento ----------

    [Fact]
    public async Task PrepararEnvio_DeveReservarEAssinarTipoETamanho()
    {
        var envio = await PrepararEnvio(5 * Mb);

        _armazenamento.EnviosPreparados[envio.Chave].Should().Be((TiposArquivoMidia.Mp4, 5 * Mb));
        (await _context.ReservasEnvio.SingleAsync()).Should().Match<ReservaEnvio>(r =>
            r.Chave == envio.Chave && r.TamanhoBytes == 5 * Mb && r.ExpiraEm == Agora + TetoArmazenamento.ValidadeReserva);
    }

    [Fact]
    public async Task PrepararEnvio_AcimaDoTetoDaRedeNaoDeveGerarUrl()
    {
        _configuracao.LimiteArmazenamentoBytes = 4 * Mb;

        var acao = () => PrepararEnvio(5 * Mb);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("TamanhoBytes");
        _armazenamento.EnviosPreparados.Should().BeEmpty();
    }

    [Fact]
    public async Task CriarMidia_ArquivoMaiorQueOReservadoDeveSerApagado()
    {
        var envio = await PrepararEnvio(1000);
        _armazenamento.Arquivos[envio.Chave] = Arquivos.Mp4(5000);

        var acao = () => CriarMidia(envio.Chave);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Single().Should().Contain("maior que o informado");
        _armazenamento.Removidos.Should().Equal(envio.Chave);
    }

    [Fact]
    public async Task CriarMidia_SemReservaDeveSerRecusadaEApagada()
    {
        var chave = ChavesArmazenamento.Gerar(_clienteId, TiposArquivoMidia.Mp4);
        _armazenamento.Arquivos[chave] = Arquivos.Mp4();

        var acao = () => CriarMidia(chave);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Single().Should().Contain("expirou");
        _armazenamento.Removidos.Should().Equal(chave);
    }

    [Fact]
    public async Task CriarMidia_DeveConsumirAReserva()
    {
        var envio = await PrepararEnvio(4000);
        _armazenamento.Arquivos[envio.Chave] = Arquivos.Mp4(4000);

        await CriarMidia(envio.Chave);

        (await _context.ReservasEnvio.AnyAsync()).Should().BeFalse("o arquivo passou a contar pela mídia");
        (await Teto().MedirAsync(CancellationToken.None)).ArquivosBytes.Should().Be(4000);
    }

    // ---------- Inventário ----------

    [Fact]
    public async Task Inventario_DeveApagarSoOrfaosAntigos()
    {
        var midia = Midia(2 * Mb);
        await _context.SaveChangesAsync();
        _armazenamento.Gravar(midia.ChaveArmazenamento, 2000, Agora.AddDays(-2));
        _armazenamento.Gravar("clientes/1/midias/abandonado.mp4", 3000, Agora.AddDays(-1));
        _armazenamento.Gravar("clientes/1/midias/chegando.mp4", 500, Agora.AddMinutes(-10));
        _armazenamento.Gravar("clientes/1/midias/reservado.mp4", 700, Agora.AddDays(-1));
        _armazenamento.Gravar("app/v1.apk", 900, Agora.AddDays(-5));
        _context.VersoesApp.Add(new VersaoApp { Codigo = 1, Nome = "1.0", ChaveArmazenamento = "app/v1.apk", TamanhoBytes = 900, Hash = new string('1', 64) });
        Reservas.Criar(_context, "clientes/1/midias/reservado.mp4", 700, Agora);

        var resultado = await Inventario().ExecutarAsync(CancellationToken.None);

        _armazenamento.Removidos.Should().Equal("clientes/1/midias/abandonado.mp4");
        resultado.OrfaosRemovidos.Should().Be(1);
        resultado.BytesOrfaosRemovidos.Should().Be(3000);
        resultado.Objetos.Should().Be(4);
        resultado.BytesNoBucket.Should().Be(2000 + 500 + 700 + 900);

        var registro = await _context.InventariosArmazenamento.SingleAsync();
        registro.VerificadoEm.Should().Be(Agora);
        registro.BytesNoBucket.Should().Be(4100);
    }

    [Fact]
    public async Task Inventario_SeAMaioriaPareceOrfaDeveApagarNada()
    {
        var midia = Midia(1000);
        await _context.SaveChangesAsync();
        _armazenamento.Gravar(midia.ChaveArmazenamento, 1000, Agora.AddDays(-2));
        _armazenamento.Gravar("clientes/1/midias/x.mp4", 1000, Agora.AddDays(-2));
        _armazenamento.Gravar("clientes/1/midias/y.mp4", 1000, Agora.AddDays(-2));

        var resultado = await Inventario().ExecutarAsync(CancellationToken.None);

        _armazenamento.Removidos.Should().BeEmpty("2 de 3 arquivos sem dono indica banco errado, não lixo");
        resultado.OrfaosRemovidos.Should().Be(0);
        resultado.BytesNoBucket.Should().Be(3000);
    }

    [Fact]
    public async Task Inventario_DeveAvisarUmaVezPorNivelEVoltarAZeroQuandoCair()
    {
        var midia = Midia(72 * Mb);
        await _context.SaveChangesAsync();

        (await Inventario().ExecutarAsync(CancellationToken.None)).AlertaEnviado.Should().Be(70);
        (await Inventario().ExecutarAsync(CancellationToken.None)).AlertaEnviado.Should().BeNull("já avisou dos 70%");
        _emails.Enfileirados.Should().ContainSingle().Which.Subject.Should().Contain("72%");

        midia.TamanhoBytes = 90 * Mb;
        await _context.SaveChangesAsync();
        (await Inventario().ExecutarAsync(CancellationToken.None)).AlertaEnviado.Should().Be(85);

        midia.TamanhoBytes = 10 * Mb;
        await _context.SaveChangesAsync();
        await Inventario().ExecutarAsync(CancellationToken.None);
        (await _context.InventariosArmazenamento.SingleAsync()).NivelAlertaAvisado.Should().Be(0);

        midia.TamanhoBytes = 96 * Mb;
        await _context.SaveChangesAsync();
        (await Inventario().ExecutarAsync(CancellationToken.None)).AlertaEnviado.Should().Be(95);
        _emails.Enfileirados.Should().HaveCount(3);
        _emails.Enfileirados[^1].HtmlBody.Should().Contain("quase no teto");
    }

    [Fact]
    public async Task Inventario_SemListagemDeveUsarABaseEApagarNada()
    {
        Midia(5 * Mb);
        await _context.SaveChangesAsync();
        var semListagem = new ArmazenamentoFake();
        semListagem.Arquivos["clientes/1/midias/orfao.mp4"] = [1];

        var resultado = await new InventarioArmazenamentoServico(_context, semListagem, Teto(), _configuracao, _emails, _relogio,
            NullLogger<InventarioArmazenamentoServico>.Instance).ExecutarAsync(CancellationToken.None);

        resultado.BytesNoBucket.Should().Be(5 * Mb);
        resultado.Objetos.Should().BeNull();
        (await Teto().MedirAsync(CancellationToken.None)).BucketBytes.Should().BeNull("sem listagem não houve medição real");
        semListagem.Removidos.Should().BeEmpty();
    }

    // ---------- Configuração ----------

    [Theory]
    [InlineData(null, 9216)]
    [InlineData("20000", 10240)]
    [InlineData("50", 100)]
    [InlineData("5000", 5000)]
    public void Limite_NuncaPassaDos10Gb(string? configurado, long esperadoMb)
    {
        var valores = new Dictionary<string, string?> { ["Indoor:UrlPainel"] = "http://localhost" };
        if (configurado is not null)
        {
            valores["Indoor:Armazenamento:LimiteTotalMb"] = configurado;
        }

        var configuracao = new Atlas.Infrastructure.Business.ConfiguracaoIndoor(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());

        configuracao.LimiteArmazenamentoBytes.Should().Be(esperadoMb * Mb);
    }

    private Midia Midia(long tamanho)
    {
        var midia = new Midia
        {
            ClienteId = _clienteId,
            Titulo = "Mídia",
            Tipo = TipoMidia.Video,
            ChaveArmazenamento = ChavesArmazenamento.Gerar(_clienteId, TiposArquivoMidia.Mp4),
            TipoConteudo = TiposArquivoMidia.Mp4,
            TamanhoBytes = tamanho,
            Hash = new string('a', 64),
            DuracaoSegundos = 15,
        };
        _context.Midias.Add(midia);
        return midia;
    }

    private Task<EnvioMidiaDto> PrepararEnvio(long tamanho) =>
        new PrepararEnvioMidiaCommandHandler(_context, new ClienteAtualFake(_clienteId), _armazenamento, Teto())
            .Handle(new PrepararEnvioMidiaCommand { TipoConteudo = TiposArquivoMidia.Mp4, TamanhoBytes = tamanho }, CancellationToken.None);

    private Task<long> CriarMidia(string chave) =>
        new CriarMidiaCommandHandler(_context, new ClienteAtualFake(_clienteId), _armazenamento, NullLogger<CriarMidiaCommandHandler>.Instance)
            .Handle(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Mp4, DuracaoSegundos = 15 }, CancellationToken.None);

    private InventarioArmazenamentoServico Inventario() =>
        new(_context, _armazenamento, Teto(), _configuracao, _emails, _relogio, NullLogger<InventarioArmazenamentoServico>.Instance);
}
