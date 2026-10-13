using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

public class DiasSemanaTests
{
    [Theory]
    [InlineData(DayOfWeek.Sunday, DiasSemana.Domingo)]
    [InlineData(DayOfWeek.Monday, DiasSemana.Segunda)]
    [InlineData(DayOfWeek.Wednesday, DiasSemana.Quarta)]
    [InlineData(DayOfWeek.Saturday, DiasSemana.Sabado)]
    public void ParaDiasSemana_DeveConverterCadaDia(DayOfWeek dia, DiasSemana esperado)
    {
        dia.ParaDiasSemana().Should().Be(esperado);
    }

    [Fact]
    public void Inclui_DeveConsiderarSoOsDiasMarcados()
    {
        var dias = DiasSemana.Segunda | DiasSemana.Quarta;

        dias.Inclui(DayOfWeek.Monday).Should().BeTrue();
        dias.Inclui(DayOfWeek.Wednesday).Should().BeTrue();
        dias.Inclui(DayOfWeek.Tuesday).Should().BeFalse();
        DiasSemana.Todos.Inclui(DayOfWeek.Sunday).Should().BeTrue();
        DiasSemana.DiasUteis.Inclui(DayOfWeek.Saturday).Should().BeFalse();
    }
}

public class MidiaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 13);

    [Fact]
    public void EstaVigente_SemPeriodo_DeveValerSempre()
    {
        new Midia().EstaVigente(Hoje).Should().BeTrue();
    }

    [Fact]
    public void EstaVigente_DeveIncluirOPrimeiroEOUltimoDia()
    {
        var midia = new Midia { InicioExibicao = Hoje, FimExibicao = Hoje };

        midia.EstaVigente(Hoje).Should().BeTrue();
        midia.EstaVigente(Hoje.AddDays(-1)).Should().BeFalse();
        midia.EstaVigente(Hoje.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void EstaVigente_Inativa_NaoDeveValer()
    {
        new Midia { Ativo = false }.EstaVigente(Hoje).Should().BeFalse();
    }
}

public class PlaylistTests
{
    [Fact]
    public void DefinirItens_DeveUsarAPosicaoNaListaComoOrdem()
    {
        var playlist = new Playlist();

        playlist.DefinirItens(
        [
            new ItemPlaylistInformado(null, MidiaId: 7, DiasSemana.Todos),
            new ItemPlaylistInformado(null, MidiaId: 3, DiasSemana.DiasUteis),
            new ItemPlaylistInformado(null, MidiaId: 7, DiasSemana.FimDeSemana),
        ]);

        playlist.Itens.Select(i => (i.MidiaId, i.Ordem, i.DiasSemana)).Should().Equal(
            (7L, 0, DiasSemana.Todos),
            (3L, 1, DiasSemana.DiasUteis),
            (7L, 2, DiasSemana.FimDeSemana));
    }

    [Fact]
    public void DefinirItens_DeveAtualizarRemoverEReordenar()
    {
        var playlist = ComItens((1, 10), (2, 20), (3, 30));

        // Inverte 3 e 1, remove 2, acrescenta uma mídia nova no fim.
        playlist.DefinirItens(
        [
            new ItemPlaylistInformado(3, MidiaId: 30, DiasSemana.Segunda),
            new ItemPlaylistInformado(1, MidiaId: 10, DiasSemana.Todos),
            new ItemPlaylistInformado(null, MidiaId: 40, DiasSemana.Todos),
        ]);

        playlist.Itens.OrderBy(i => i.Ordem).Select(i => (i.Id, i.MidiaId, i.Ordem)).Should().Equal(
            (3L, 30L, 0),
            (1L, 10L, 1),
            (0L, 40L, 2));
        playlist.Itens.Single(i => i.Id == 3).DiasSemana.Should().Be(DiasSemana.Segunda);
    }

    [Fact]
    public void DefinirItens_DeveRecusarItemDeOutraPlaylist()
    {
        var playlist = ComItens((1, 10));

        var acao = () => playlist.DefinirItens([new ItemPlaylistInformado(99, MidiaId: 10, DiasSemana.Todos)]);

        acao.Should().Throw<InvalidOperationException>();
        playlist.Itens.Should().ContainSingle(i => i.Id == 1);
    }

    [Fact]
    public void DefinirItens_DeveRecusarItemSemDia()
    {
        var acao = () => new Playlist().DefinirItens([new ItemPlaylistInformado(null, MidiaId: 10, DiasSemana.Nenhum)]);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefinirItens_ListaVazia_DeveEsvaziarAPlaylist()
    {
        var playlist = ComItens((1, 10), (2, 20));

        playlist.DefinirItens([]);

        playlist.Itens.Should().BeEmpty();
    }

    private static Playlist ComItens(params (long Id, long MidiaId)[] itens)
    {
        var playlist = new Playlist();
        playlist.Itens.AddRange(itens.Select((i, ordem) => new ItemPlaylist { Id = i.Id, MidiaId = i.MidiaId, Ordem = ordem }));
        return playlist;
    }
}

public class TelaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(15);

    [Fact]
    public void CodigoPareamento_DeveValerAteExpirar()
    {
        var tela = new Tela();
        tela.GerarCodigoPareamento("AB12CD34", Agora);

        tela.CodigoPareamentoValido("AB12CD34", Agora.AddHours(23)).Should().BeTrue();
        tela.CodigoPareamentoValido("AB12CD34", Agora.Add(Tela.ValidadeCodigoPareamento)).Should().BeFalse();
        tela.CodigoPareamentoValido("ab12cd34", Agora).Should().BeFalse();
    }

    [Fact]
    public void CodigoPareamento_TelaInativa_NaoDeveValer()
    {
        var tela = new Tela { Ativo = false };
        tela.GerarCodigoPareamento("AB12CD34", Agora);

        tela.CodigoPareamentoValido("AB12CD34", Agora).Should().BeFalse();
    }

    [Fact]
    public void GerarCodigoPareamento_DeveRecusarTamanhoErrado()
    {
        var acao = () => new Tela().GerarCodigoPareamento("ABC", Agora);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parear_DeveConsumirOCodigoEInvalidarTokensAnteriores()
    {
        var tela = new Tela();
        tela.GerarCodigoPareamento("AB12CD34", Agora);
        var versaoAntes = tela.VersaoCredencial;

        tela.Parear("tv-academia-01", Agora);

        tela.Pareada.Should().BeTrue();
        tela.DispositivoId.Should().Be("tv-academia-01");
        tela.PareadaEm.Should().Be(Agora);
        tela.CodigoPareamento.Should().BeNull();
        tela.CodigoPareamentoExpiraEm.Should().BeNull();
        tela.VersaoCredencial.Should().Be(versaoAntes + 1);
    }

    [Fact]
    public void Desvincular_DeveTirarATvEInvalidarOToken()
    {
        var tela = new Tela();
        tela.Parear("tv-academia-01", Agora);
        var versaoAntes = tela.VersaoCredencial;

        tela.Desvincular();

        tela.Pareada.Should().BeFalse();
        tela.PareadaEm.Should().BeNull();
        tela.VersaoCredencial.Should().Be(versaoAntes + 1);
    }

    [Fact]
    public void Situacao_DeveSeguirOPareamentoEAUltimaSincronizacao()
    {
        var tela = new Tela();
        tela.Situacao(Agora, Tolerancia).Should().Be(SituacaoTela.AguardandoPareamento);

        tela.Parear("tv-academia-01", Agora);
        tela.Situacao(Agora, Tolerancia).Should().Be(SituacaoTela.Offline, "pareada, mas ainda não sincronizou");

        tela.Estado = new EstadoTela();
        tela.Estado.RegistrarSincronizacao(Agora, "1.0.0", "versao", itensProntos: 3);
        tela.Situacao(Agora.Add(Tolerancia), Tolerancia).Should().Be(SituacaoTela.Online);
        tela.Situacao(Agora.Add(Tolerancia).AddSeconds(1), Tolerancia).Should().Be(SituacaoTela.Offline);
    }
}
