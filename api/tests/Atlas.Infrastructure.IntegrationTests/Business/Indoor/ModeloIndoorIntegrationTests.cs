using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Application.Business.Diretorio;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
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
    private long _clienteId;
    private long _parceiroId;

    public async Task InitializeAsync()
    {
        await BancoDeTestes.AtualizarAsync(fixture);

        await using var context = fixture.CreateDbContext();
        var cliente = new Cliente { Nome = $"Academia {_sufixo}", Email = $"{_sufixo}@teste.local" };
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();
        _clienteId = cliente.Id;

        // O local das TVs deste teste; o dono também anuncia (diretório: vai primeiro).
        var parceiro = new Parceiro { Nome = $"Academia {_sufixo}", ClienteId = cliente.Id };
        context.Parceiros.Add(parceiro);
        await context.SaveChangesAsync();
        _parceiroId = parceiro.Id;
    }

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.Telas.Where(t => t.ParceiroId == _parceiroId).ExecuteDeleteAsync();
        // Playlists são da rede (sem cliente): as deste teste levam o sufixo no nome.
        await context.Playlists.Where(p => p.Nome.EndsWith(_sufixo)).ExecuteDeleteAsync();
        await context.Midias.Where(m => m.ClienteId == _clienteId).ExecuteDeleteAsync();
        await context.Parceiros.Where(p => p.Id == _parceiroId).ExecuteDeleteAsync();
        await context.Clientes.Where(p => p.Id == _clienteId).ExecuteDeleteAsync();
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

    [Fact]
    public async Task Diretorio_DeveSairDoPostgresComOHostPrimeiroESoQuemAceitou()
    {
        var codigo = ("D" + _sufixo.ToUpperInvariant().Replace('0', 'A').Replace('1', 'B') + "Z")[..Tela.TamanhoCodigoDiretorio];
        var midia = await CriarMidiaAsync();

        await using (var context = fixture.CreateDbContext())
        {
            var host = await context.Clientes.SingleAsync(p => p.Id == _clienteId);
            host.DefinirExibicaoNoDiretorio(true, DateTimeOffset.UtcNow);
            host.WhatsappPublico = "5592999990000";
            var playlist = new Playlist { Nome = $"Grade {_sufixo}" };
            playlist.DefinirItens([new ItemPlaylistInformado(null, midia, DiasSemana.Todos)]);
            var tela = new Tela { ParceiroId = _parceiroId, Nome = $"Balcão {_sufixo}", Playlist = playlist, DiretorioAtivo = true };
            tela.DefinirCodigoDiretorio(codigo);
            context.Telas.Add(tela);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var diretorio = await new GetDiretorioPublicoQueryHandler(context, TimeProvider.System)
                .Handle(new GetDiretorioPublicoQuery(codigo.ToLowerInvariant()), CancellationToken.None);

            diretorio.Anunciantes.Should().ContainSingle();
            diretorio.Anunciantes[0].Anfitriao.Should().BeTrue();
            diretorio.Anunciantes[0].Contatos.Single().Valor.Should().Be("https://wa.me/5592999990000");
        }
    }

    [Theory]
    [InlineData("ck_tela_diretorio")]
    [InlineData("ck_cliente_diretorio_aceite")]
    [InlineData("ck_cliente_whatsapp_publico")]
    public async Task Diretorio_DeveRecusarDadosInvalidosNoBanco(string restricao)
    {
        await using var context = fixture.CreateDbContext();

        Func<Task> acao = restricao switch
        {
            // Diretório ligado sem endereço.
            "ck_tela_diretorio" => () =>
            {
                context.Telas.Add(new Tela { ParceiroId = _parceiroId, Nome = $"TV {_sufixo}", DiretorioAtivo = true });
                return context.SaveChangesAsync();
            },
            // Aceite sem data.
            "ck_cliente_diretorio_aceite" => () => context.Database.ExecuteSqlAsync(
                $"UPDATE business.cliente SET exibir_no_diretorio_publico = true WHERE id = {_clienteId}"),
            _ => () => context.Database.ExecuteSqlAsync(
                $"UPDATE business.cliente SET whatsapp_publico = '92 9999' WHERE id = {_clienteId}"),
        };

        var excecao = await acao.Should().ThrowAsync<Exception>();
        (excecao.Which as PostgresException ?? excecao.Which.InnerException as PostgresException)!
            .ConstraintName.Should().Be(restricao);
    }

    [Fact]
    public async Task Veiculacao_ReenvioDaMesmaExibicaoEDadosInvalidosSaoRecusadosNoBanco()
    {
        var telaFicticia = -Random.Shared.NextInt64(1, long.MaxValue);
        var idLocal = Guid.NewGuid();
        RegistroExibicao Registro() => new()
        {
            TelaId = telaFicticia, ParceiroId = _parceiroId, MidiaId = 1, ClienteId = _clienteId,
            IdLocal = idLocal, Inicio = DateTimeOffset.UtcNow, DuracaoMs = 15_000, Concluida = true, RecebidoEm = DateTimeOffset.UtcNow,
        };

        try
        {
            await using (var context = fixture.CreateDbContext())
            {
                context.RegistrosExibicao.Add(Registro());
                await context.SaveChangesAsync();
            }

            await using (var context = fixture.CreateDbContext())
            {
                context.RegistrosExibicao.Add(Registro());
                var repetido = () => context.SaveChangesAsync();
                (await repetido.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
                    .Which.ConstraintName.Should().Be("ix_registro_exibicao_tela_id_id_local");
            }

            await using (var context = fixture.CreateDbContext())
            {
                context.LeiturasQr.Add(new LeituraQr { MidiaId = 1, ClienteId = _clienteId, Momento = DateTimeOffset.UtcNow, Visitante = "200.1.2.3" });
                var comIp = () => context.SaveChangesAsync();
                (await comIp.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
                    .Which.ConstraintName.Should().Be("ck_leitura_qr_visitante");
            }

            await using (var context = fixture.CreateDbContext())
            {
                var midia = NovaMidia();
                midia.DefinirCodigoQr("ABCD2345");
                context.Midias.Add(midia);
                var semQr = () => context.SaveChangesAsync();
                (await semQr.Should().ThrowAsync<DbUpdateException>()).WithInnerException<PostgresException>()
                    .Which.ConstraintName.Should().Be("ck_midia_codigo_qr");
            }
        }
        finally
        {
            await using var context = fixture.CreateDbContext();
            await context.RegistrosExibicao.Where(r => r.TelaId == telaFicticia).ExecuteDeleteAsync();
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
        ClienteId = _clienteId,
        Titulo = "Promoção de inverno",
        Tipo = TipoMidia.Video,
        ChaveArmazenamento = $"clientes/{_clienteId}/midias/{Guid.NewGuid():N}.mp4",
        TipoConteudo = "video/mp4",
        TamanhoBytes = 2_500_000,
        Hash = new string('a', 64),
    };
}
