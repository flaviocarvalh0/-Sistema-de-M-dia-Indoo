using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Npgsql;

namespace Atlas.Infrastructure.IntegrationTests.Business.Indoor;

/// <summary>
/// O modelo Indoor no PostgreSQL real: as regras que o banco garante (check
/// constraints, índice único filtrado do código de pareamento, cascata dos
/// itens da playlist) e o mapeamento de <see cref="DateOnly"/> e das flags de
/// <see cref="DiasSemana"/> — o InMemory dos testes unitários não garante nada disso.
/// </summary>
[Collection("DatabaseCollection")]
public class ModeloIndoorIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];
    private long _parceiroId;

    public async Task InitializeAsync()
    {
        await BancoDeTestes.AtualizarAsync(fixture);

        await using var context = fixture.CreateDbContext();
        var parceiro = new Parceiro { Nome = $"Academia {_sufixo}", Email = $"{_sufixo}@teste.local" };
        context.Parceiros.Add(parceiro);
        await context.SaveChangesAsync();
        _parceiroId = parceiro.Id;
    }

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.Telas.Where(t => t.ParceiroId == _parceiroId).ExecuteDeleteAsync();
        // Playlists são da rede (sem parceiro): as deste teste levam o sufixo no nome.
        await context.Playlists.Where(p => p.Nome.EndsWith(_sufixo)).ExecuteDeleteAsync();
        await context.Midias.Where(m => m.ParceiroId == _parceiroId).ExecuteDeleteAsync();
        await context.Parceiros.Where(p => p.Id == _parceiroId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Playlist_DeveGravarItensNaOrdemERemoverOsRetiradosDaLista()
    {
        var (midiaA, midiaB) = (await CriarMidiaAsync(), await CriarMidiaAsync());

        long playlistId;
        await using (var context = fixture.CreateDbContext())
        {
            var playlist = new Playlist { Nome = $"Grade principal {_sufixo}" };
            playlist.DefinirItens(
            [
                new ItemPlaylistInformado(null, midiaA, DiasSemana.DiasUteis),
                new ItemPlaylistInformado(null, midiaB, DiasSemana.Sabado | DiasSemana.Domingo),
            ]);
            context.Playlists.Add(playlist);
            await context.SaveChangesAsync();
            playlistId = playlist.Id;
        }

        await using (var context = fixture.CreateDbContext())
        {
            var playlist = await context.Playlists.Include(p => p.Itens).SingleAsync(p => p.Id == playlistId);
            var itemB = playlist.Itens.Single(i => i.MidiaId == midiaB);

            // Fica só B, agora em primeiro.
            playlist.DefinirItens([new ItemPlaylistInformado(itemB.Id, midiaB, DiasSemana.Todos)]);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var itens = await context.ItensPlaylist.Where(i => i.PlaylistId == playlistId).ToListAsync();

            itens.Should().ContainSingle();
            itens[0].MidiaId.Should().Be(midiaB);
            itens[0].Ordem.Should().Be(0);
            itens[0].DiasSemana.Should().Be(DiasSemana.Todos);
        }
    }

    [Fact]
    public async Task Midia_DevePersistirOPeriodoDeExibicaoComoData()
    {
        var id = await CriarMidiaAsync(inicio: new DateOnly(2026, 11, 1), fim: new DateOnly(2026, 11, 30));

        await using var context = fixture.CreateDbContext();
        var midia = await context.Midias.SingleAsync(m => m.Id == id);

        midia.InicioExibicao.Should().Be(new DateOnly(2026, 11, 1));
        midia.FimExibicao.Should().Be(new DateOnly(2026, 11, 30));
        midia.EstaVigente(new DateOnly(2026, 11, 30)).Should().BeTrue();
    }

    [Fact]
    public async Task Midia_DevePersistirEApagarOQrCode()
    {
        var id = await CriarMidiaAsync();
        var pix = new QrCodeDaMidia(
            TipoQrCode.Pix, "+5592999990000", null, 12.5m, "Padaria Centro", "Manaus", "Pague com Pix",
            PosicaoQrCode.SuperiorEsquerdo, TamanhoQrCode.Grande, "000201...6304ABCD");

        await using (var context = fixture.CreateDbContext())
        {
            (await context.Midias.SingleAsync(m => m.Id == id)).DefinirQrCode(pix);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var midia = await context.Midias.SingleAsync(m => m.Id == id);
            midia.QrTipo.Should().Be(TipoQrCode.Pix);
            midia.QrValor.Should().Be(12.5m);
            midia.QrPosicao.Should().Be(PosicaoQrCode.SuperiorEsquerdo);
            midia.QrTamanho.Should().Be(TamanhoQrCode.Grande);
            midia.QrConteudo.Should().Be("000201...6304ABCD");
            midia.RemoverQrCode();
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var midia = await context.Midias.SingleAsync(m => m.Id == id);
            midia.QrTipo.Should().BeNull();
            midia.QrConteudo.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("ck_midia_qr_code_completo")]
    [InlineData("ck_midia_qr_valor")]
    public async Task Midia_DeveRecusarQrCodeInvalidoNoBanco(string restricao)
    {
        var id = await CriarMidiaAsync();
        await using var context = fixture.CreateDbContext();

        // Tipo sem conteúdo (QR pela metade) ou valor do Pix zerado.
        var acao = () => restricao == "ck_midia_qr_code_completo"
            ? context.Database.ExecuteSqlAsync($"UPDATE indoor.midia SET qr_tipo = 1 WHERE id = {id}")
            : context.Database.ExecuteSqlAsync($"UPDATE indoor.midia SET qr_valor = 0 WHERE id = {id}");

        (await acao.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be(restricao);
    }

    [Theory]
    [InlineData("ck_midia_hash")]
    [InlineData("ck_midia_periodo")]
    [InlineData("ck_midia_tamanho")]
    public async Task Midia_DeveRecusarDadosInvalidosNoBanco(string restricao)
    {
        await using var context = fixture.CreateDbContext();
        var midia = NovaMidia();

        switch (restricao)
        {
            case "ck_midia_hash":
                midia.Hash = new string('X', 64);
                break;
            case "ck_midia_periodo":
                midia.InicioExibicao = new DateOnly(2026, 11, 30);
                midia.FimExibicao = new DateOnly(2026, 11, 1);
                break;
            case "ck_midia_tamanho":
                midia.TamanhoBytes = Midia.TamanhoMaximoBytes + 1;
                break;
        }

        context.Midias.Add(midia);
        var acao = () => context.SaveChangesAsync();

        (await acao.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be(restricao);
    }

    [Fact]
    public async Task Item_DeveRecusarDiasSemanaVazio()
    {
        var midia = await CriarMidiaAsync();

        await using var context = fixture.CreateDbContext();
        var playlist = new Playlist { Nome = $"Sem dias {_sufixo}" };
        playlist.Itens.Add(new ItemPlaylist { MidiaId = midia, DiasSemana = DiasSemana.Nenhum });
        context.Playlists.Add(playlist);

        var acao = () => context.SaveChangesAsync();

        (await acao.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("ck_item_playlist_dias_semana");
    }

    [Fact]
    public async Task CodigoPareamento_DeveSerUnicoSoEntreAsTelasComCodigo()
    {
        var agora = DateTimeOffset.UtcNow;
        var codigo = _sufixo.ToUpperInvariant();

        await using (var context = fixture.CreateDbContext())
        {
            var comCodigo = new Tela { ParceiroId = _parceiroId, Nome = "Recepção" };
            comCodigo.GerarCodigoPareamento(codigo, agora);

            // Várias telas sem código convivem (índice filtrado).
            context.Telas.AddRange(
                comCodigo,
                new Tela { ParceiroId = _parceiroId, Nome = "Musculação" },
                new Tela { ParceiroId = _parceiroId, Nome = "Vestiário" });
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var repetida = new Tela { ParceiroId = _parceiroId, Nome = "Cardio" };
            repetida.GerarCodigoPareamento(codigo, agora);
            context.Telas.Add(repetida);

            var acao = () => context.SaveChangesAsync();

            (await acao.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<PostgresException>()
                .Which.ConstraintName.Should().Be("ix_tela_codigo_pareamento");
        }
    }

    [Fact]
    public async Task Tela_DeveGravarEstadoSeparadoEApagarJunto()
    {
        long telaId;
        await using (var context = fixture.CreateDbContext())
        {
            var tela = new Tela { ParceiroId = _parceiroId, Nome = "Recepção" };
            tela.Parear($"tv-{_sufixo}", DateTimeOffset.UtcNow);
            tela.Estado = new EstadoTela();
            tela.Estado.RegistrarSincronizacao(DateTimeOffset.UtcNow, "1.0.0", new string('a', 64), itensProntos: 2);
            context.Telas.Add(tela);
            await context.SaveChangesAsync();
            telaId = tela.Id;
        }

        await using (var context = fixture.CreateDbContext())
        {
            (await context.EstadosTela.AnyAsync(e => e.TelaId == telaId)).Should().BeTrue();

            await context.Telas.Where(t => t.Id == telaId).ExecuteDeleteAsync();

            (await context.EstadosTela.AnyAsync(e => e.TelaId == telaId)).Should().BeFalse();
        }
    }

    private async Task<long> CriarMidiaAsync(DateOnly? inicio = null, DateOnly? fim = null)
    {
        await using var context = fixture.CreateDbContext();
        var midia = NovaMidia();
        midia.InicioExibicao = inicio;
        midia.FimExibicao = fim;
        context.Midias.Add(midia);
        await context.SaveChangesAsync();
        return midia.Id;
    }

    private Midia NovaMidia() => new()
    {
        ParceiroId = _parceiroId,
        Titulo = "Promoção de inverno",
        Tipo = TipoMidia.Video,
        ChaveArmazenamento = $"parceiros/{_parceiroId}/midias/{Guid.NewGuid():N}.mp4",
        TipoConteudo = "video/mp4",
        TamanhoBytes = 2_500_000,
        Hash = new string('a', 64),
    };
}
