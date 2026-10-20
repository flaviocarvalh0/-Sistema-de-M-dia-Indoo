using Atlas.Application.Business.Rede.Telas.Alertas;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.UnitTests.Business.Indoor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Rede;

/// <summary>Alerta por e-mail de TV fora do ar (ADR-020).</summary>
public class AlertaTelasOfflineTests : IDisposable
{
    // 14h em Manaus (18h UTC): dentro do horário de alertas (7h às 22h).
    private static readonly DateTimeOffset Agora = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly RelogioFixo _relogio = new(Agora);
    private readonly EmailsFake _emails = new();
    private readonly ConfiguracaoIndoorFake _configuracao = new();
    private readonly Parceiro _host;

    public AlertaTelasOfflineTests()
    {
        _host = new Parceiro { Nome = "Academia", Email = "a@teste.local", FusoHorario = "America/Manaus" };
        _context.Parceiros.Add(_host);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task TvSemSincronizarAlemDaTolerancia_UmAlertaSo_EAvisoQuandoVolta()
    {
        var tela = Tela("Recepção", ultima: Agora.AddMinutes(-20));
        Tela("Balcão", ultima: Agora.AddMinutes(-2));

        var primeira = await Verificar();
        primeira.Cairam.Select(t => t.Nome).Should().Equal("Recepção");
        _emails.Enfileirados.Single().Subject.Should().Be("1 TV(s) fora do ar");
        _emails.Enfileirados.Single().HtmlBody.Should().Contain("Academia");

        (await Verificar()).Cairam.Should().BeEmpty("o alerta em aberto não se repete");
        _emails.Enfileirados.Should().HaveCount(1);

        tela.Estado!.UltimaSincronizacao = Agora.AddMinutes(1);
        await _context.SaveChangesAsync();
        _relogio.Agora = Agora.AddMinutes(5);

        var volta = await Verificar();
        volta.Voltaram.Select(t => t.Nome).Should().Equal("Recepção");
        _emails.Enfileirados.Last().Subject.Should().Be("1 TV(s) de volta ao ar");
        (await _context.EstadosTela.SingleAsync(e => e.TelaId == tela.Id)).AlertaOfflineEm.Should().BeNull();
    }

    [Fact]
    public async Task ForaDoHorarioNoFusoDoEstabelecimento_NaoAlerta_EAlertaQuandoOHorarioAbre()
    {
        // 2h em Manaus.
        _relogio.Agora = new DateTimeOffset(2026, 10, 16, 6, 0, 0, TimeSpan.Zero);
        Tela("Recepção", ultima: _relogio.Agora.AddHours(-3));

        (await Verificar()).Cairam.Should().BeEmpty();
        _emails.Enfileirados.Should().BeEmpty();

        // 7h em Manaus: ainda fora do ar.
        _relogio.Agora = new DateTimeOffset(2026, 10, 16, 11, 0, 0, TimeSpan.Zero);
        (await Verificar()).Cairam.Should().ContainSingle();
    }

    [Fact]
    public async Task TelaInativaOuSemTvPareada_NaoAlerta()
    {
        var inativa = Tela("Inativa", ultima: Agora.AddHours(-1));
        inativa.Ativo = false;
        var desvinculada = Tela("Desvinculada", ultima: Agora.AddHours(-1));
        desvinculada.Desvincular();
        await _context.SaveChangesAsync();

        (await Verificar()).Cairam.Should().BeEmpty();
    }

    [Fact]
    public async Task SemEmailConfigurado_MarcaSemEnviar()
    {
        _configuracao.EmailsOperadora = [];
        Tela("Recepção", ultima: Agora.AddHours(-1));

        (await Verificar()).Cairam.Should().ContainSingle();
        _emails.Enfileirados.Should().BeEmpty();
    }

    [Fact]
    public async Task TvSincronizandoSemExibir_AlertaDepoisDaTolerancia_EAvisoQuandoVoltaAExibir()
    {
        var tela = Tela("Recepção", ultima: Agora.AddMinutes(-1));
        // App fora da tela desde 12 min atrás.
        tela.Estado!.RegistrarReproducao(Agora.AddMinutes(-12), ModoReproducao.Anuncio, Agora.AddMinutes(-12), 15, 1, primeiroPlano: false, 3, Agora.AddMinutes(-12));
        var recente = Tela("Balcão", ultima: Agora.AddMinutes(-1));
        recente.Estado!.RegistrarReproducao(Agora.AddMinutes(-3), ModoReproducao.Espera, Agora.AddMinutes(-3), null, null, true, 2, Agora.AddMinutes(-3));
        await _context.SaveChangesAsync();

        var primeira = await Verificar();
        primeira.PararamDeExibir.Select(t => t.Nome).Should().Equal(["Recepção"], "o Balcão ainda está dentro dos 10 min");
        primeira.PararamDeExibir[0].Detalhe.Should().Contain("app saiu da tela");
        primeira.Cairam.Should().BeEmpty("ela sincroniza");
        _emails.Enfileirados.Single().Subject.Should().Be("1 TV(s) sem exibir");

        (await Verificar()).PararamDeExibir.Should().BeEmpty("o alerta em aberto não se repete");

        tela.Estado.RegistrarReproducao(Agora, ModoReproducao.Anuncio, Agora, 15, 1, true, 3, Agora);
        await _context.SaveChangesAsync();
        var volta = await Verificar();
        volta.VoltaramAExibir.Select(t => t.Nome).Should().Equal("Recepção");
        _emails.Enfileirados.Last().Subject.Should().Contain("exibindo de novo");
    }

    private Task<VerificadorTelasOffline.Resultado> Verificar() =>
        new VerificadorTelasOffline(_context, _emails, _configuracao, _relogio, NullLogger<VerificadorTelasOffline>.Instance)
            .VerificarAsync(CancellationToken.None);

    private Tela Tela(string nome, DateTimeOffset ultima)
    {
        var tela = new Tela { Parceiro = _host, Nome = nome };
        tela.Parear($"tv-{nome}", ultima.AddDays(-1));
        tela.Estado = new EstadoTela();
        tela.Estado.RegistrarSincronizacao(ultima, "1.0.0", "v", 1);
        _context.Telas.Add(tela);
        _context.SaveChanges();
        return tela;
    }
}
