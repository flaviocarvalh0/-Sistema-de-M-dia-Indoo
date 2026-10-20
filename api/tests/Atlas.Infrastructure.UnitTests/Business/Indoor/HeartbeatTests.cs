using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Heartbeat de reprodução (ADR-024): quando a TV sincroniza mas não exibe.</summary>
public class HeartbeatTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnuncioDentroDoTempo_Exibindo()
    {
        var e = new EstadoTela();
        e.RegistrarReproducao(Agora, ModoReproducao.Anuncio, Agora.AddSeconds(-10), 15, 7, true, 3, Agora);

        e.ForaDeExibicaoDesde.Should().BeNull();
        e.MidiaAtualId.Should().Be(7);
        e.RelogioErrado.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, ModoReproducao.Anuncio, 0, 3, MotivoForaDeExibicao.AppForaDaTela)]
    [InlineData(true, ModoReproducao.Anuncio, 10, 3, MotivoForaDeExibicao.ReproducaoTravada)]
    [InlineData(true, ModoReproducao.Informativo, 10, 3, MotivoForaDeExibicao.ReproducaoTravada)]
    [InlineData(true, ModoReproducao.Espera, 30, 2, MotivoForaDeExibicao.EsperaComAnuncios)]
    public void ForaDeExibicao_PorMotivo(bool primeiroPlano, ModoReproducao modo, int minutosNaTela, int itensHoje, MotivoForaDeExibicao motivo)
    {
        var e = new EstadoTela();
        e.RegistrarReproducao(Agora, modo, Agora.AddMinutes(-minutosNaTela), modo == ModoReproducao.Espera ? null : 15, null, primeiroPlano, itensHoje, Agora);

        e.MotivoForaDeExibicao.Should().Be(motivo);
        e.ForaDeExibicaoDesde.Should().Be(Agora);
    }

    [Fact]
    public void EsperaSemAnunciosParaHoje_NaoEProblema()
    {
        var e = new EstadoTela();
        e.RegistrarReproducao(Agora, ModoReproducao.Espera, Agora.AddHours(-3), null, null, true, 0, Agora);

        e.ForaDeExibicaoDesde.Should().BeNull();
    }

    [Fact]
    public void ForaDeExibicao_MantemOInicio_ELimpaQuandoVolta()
    {
        var e = new EstadoTela();
        e.RegistrarReproducao(Agora, ModoReproducao.Anuncio, Agora, 15, 1, false, 1, Agora);
        e.RegistrarReproducao(Agora.AddMinutes(5), ModoReproducao.Anuncio, Agora, 15, 1, false, 1, Agora.AddMinutes(5));
        e.ForaDeExibicaoDesde.Should().Be(Agora, "conta desde a primeira vez");

        e.RegistrarReproducao(Agora.AddMinutes(6), ModoReproducao.Anuncio, Agora.AddMinutes(6), 15, 1, true, 1, Agora.AddMinutes(6));
        e.ForaDeExibicaoDesde.Should().BeNull();
        e.MotivoForaDeExibicao.Should().BeNull();
    }

    [Fact]
    public void RelogioDaTvErrado_NaoCriaFalsoTravamento_MasFicaMarcado()
    {
        var e = new EstadoTela();
        // TV 2 h atrasada: o item começou 10 s antes do "agora" dela.
        var relogioTv = Agora.AddHours(-2);
        e.RegistrarReproducao(Agora, ModoReproducao.Anuncio, relogioTv.AddSeconds(-10), 15, 1, true, 1, relogioTv);

        e.ForaDeExibicaoDesde.Should().BeNull();
        e.DesvioRelogioSegundos.Should().Be(-7200);
        e.RelogioErrado.Should().BeTrue();
    }

    [Fact]
    public void Situacao_ForaDeExibicaoSoDepoisDaTolerancia()
    {
        var sinc = Agora.AddMinutes(-1);
        Tela.CalcularSituacao(true, sinc, Agora, Tela.ToleranciaOffline, Agora.AddMinutes(-5)).Should().Be(SituacaoTela.Online);
        Tela.CalcularSituacao(true, sinc, Agora, Tela.ToleranciaOffline, Agora.AddMinutes(-10)).Should().Be(SituacaoTela.ForaDeExibicao);
        Tela.CalcularSituacao(true, Agora.AddMinutes(-30), Agora, Tela.ToleranciaOffline, Agora.AddMinutes(-40)).Should().Be(SituacaoTela.Offline, "sem sincronizar, é Offline");
        Tela.CalcularSituacao(false, null, Agora, Tela.ToleranciaOffline).Should().Be(SituacaoTela.AguardandoPareamento);
    }
}
