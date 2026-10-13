using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Queries;
using Atlas.Application.Business.Indoor.Playlists.Commands.DefinirItensPlaylist;
using Atlas.Application.Business.Indoor.Playlists.Queries;
using Atlas.Application.Business.Indoor.Telas.Commands.Pareamento;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Business.Parceiros.Enums;
using Moq;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace Atlas.Infrastructure.IntegrationTests.Business.Indoor;

/// <summary>
/// O painel do parceiro no PostgreSQL real: isolamento entre parceiros, as
/// consultas com contagens, somas e a situação da tela traduzidas pelo Npgsql
/// e a concorrência pelo <c>xmin</c> — inclusive quando só os itens da
/// playlist mudam.
/// </summary>
[Collection("DatabaseCollection")]
public class PainelIndoorIntegrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly IIdentityService Identity = IdentitySemNomes();

    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..8];
    private long _parceiroA;
    private long _parceiroB;

    public async Task InitializeAsync()
    {
        await BancoDeTestes.AtualizarAsync(fixture);

        await using var context = fixture.CreateDbContext();
        var a = new Parceiro { Nome = $"A {_sufixo}", Email = $"a{_sufixo}@teste.local" };
        var b = new Parceiro { Nome = $"B {_sufixo}", Email = $"b{_sufixo}@teste.local" };
        context.Parceiros.AddRange(a, b);
        await context.SaveChangesAsync();
        (_parceiroA, _parceiroB) = (a.Id, b.Id);
    }

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        long[] ids = [_parceiroA, _parceiroB];
        await context.Telas.Where(t => ids.Contains(t.ParceiroId)).ExecuteDeleteAsync();
        await context.Playlists.Where(p => ids.Contains(p.ParceiroId)).ExecuteDeleteAsync();
        await context.Midias.Where(m => ids.Contains(m.ParceiroId)).ExecuteDeleteAsync();
        await context.Parceiros.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Consultas_DevemContarSomarEIsolarPorParceiro()
    {
        var (video, imagem) = (await CriarMidiaAsync(_parceiroA, 30), await CriarMidiaAsync(_parceiroA, 10));
        await CriarMidiaAsync(_parceiroB, 99);
        var playlist = await CriarPlaylistAsync(_parceiroA, "Grade", video, imagem, video);

        await using (var context = fixture.CreateDbContext())
        {
            context.Telas.AddRange(
                new Tela { ParceiroId = _parceiroA, Nome = "Recepção", PlaylistId = playlist },
                new Tela { ParceiroId = _parceiroA, Nome = "Musculação", PlaylistId = playlist },
                new Tela { ParceiroId = _parceiroB, Nome = "Balcão" });
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var midias = await new GetMidiasQueryHandler(context, Atual(_parceiroA), Identity)
                .Handle(new GetMidiasQuery { PaginationParams = new PaginationParams { PageSize = 50 } }, CancellationToken.None);
            midias.Items.Should().HaveCount(2);
            midias.Items.Single(m => m.Id == video).QuantidadePlaylists.Should().Be(1, "aparece duas vezes, mas numa playlist só");

            var grade = await new GetPlaylistByIdQueryHandler(context, Atual(_parceiroA), Identity)
                .Handle(new GetPlaylistByIdQuery(playlist), CancellationToken.None);
            grade.QuantidadeItens.Should().Be(3);
            grade.QuantidadeTelas.Should().Be(2);
            grade.DuracaoTotalSegundos.Should().Be(70);
            grade.Itens!.Select(i => i.MidiaId).Should().Equal(video, imagem, video);

            var outro = () => new GetPlaylistByIdQueryHandler(context, Atual(_parceiroB), Identity)
                .Handle(new GetPlaylistByIdQuery(playlist), CancellationToken.None);
            await outro.Should().ThrowAsync<NotFoundException>();
        }
    }

    [Fact]
    public async Task DefinirItens_DeveGerarNovaVersaoERecusarVersaoAntiga()
    {
        var midia = await CriarMidiaAsync(_parceiroA, 15);
        var playlist = await CriarPlaylistAsync(_parceiroA, "Grade");

        uint versaoInicial;
        await using (var context = fixture.CreateDbContext())
        {
            versaoInicial = context.Entry(await context.Playlists.SingleAsync(p => p.Id == playlist)).Property<uint>("Version").CurrentValue;
        }

        uint novaVersao;
        await using (var context = fixture.CreateDbContext())
        {
            novaVersao = await new DefinirItensPlaylistCommandHandler(context, Atual(_parceiroA)).Handle(new DefinirItensPlaylistCommand
            {
                Id = playlist,
                Itens = [new(null, midia, DiasSemana.Todos)],
                Version = versaoInicial,
            }, CancellationToken.None);
        }

        novaVersao.Should().NotBe(versaoInicial, "mudar só os itens também é uma edição da playlist");

        await using (var context = fixture.CreateDbContext())
        {
            // Outra pessoa, com a tela aberta desde antes, tenta salvar por cima.
            var acao = () => new DefinirItensPlaylistCommandHandler(context, Atual(_parceiroA)).Handle(new DefinirItensPlaylistCommand
            {
                Id = playlist,
                Itens = [],
                Version = versaoInicial,
            }, CancellationToken.None);

            // No pipeline do MediatR isto vira ConcurrencyException (409).
            await acao.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        await using (var context = fixture.CreateDbContext())
        {
            (await context.ItensPlaylist.CountAsync(i => i.PlaylistId == playlist)).Should().Be(1);
        }
    }

    [Fact]
    public async Task Telas_DevemCalcularSituacaoECodigoNoBanco()
    {
        var agora = DateTimeOffset.UtcNow;
        long aguardando, online, offline;

        await using (var context = fixture.CreateDbContext())
        {
            var semTv = new Tela { ParceiroId = _parceiroA, Nome = "Sem TV" };
            semTv.GerarCodigoPareamento(GeradorCodigoPareamento.Gerar(), agora);

            var noAr = new Tela { ParceiroId = _parceiroA, Nome = "No ar" };
            noAr.Parear($"tv-{_sufixo}-1", agora.AddDays(-1));
            noAr.Estado = new EstadoTela();
            noAr.Estado.RegistrarSincronizacao(agora.AddMinutes(-3), "1.0.0", new string('a', 64), 2);

            var foraDoAr = new Tela { ParceiroId = _parceiroA, Nome = "Fora do ar" };
            foraDoAr.Parear($"tv-{_sufixo}-2", agora.AddDays(-1));
            foraDoAr.Estado = new EstadoTela();
            foraDoAr.Estado.RegistrarSincronizacao(agora.AddHours(-1), "1.0.0", new string('a', 64), 2);

            context.Telas.AddRange(semTv, noAr, foraDoAr);
            await context.SaveChangesAsync();
            (aguardando, online, offline) = (semTv.Id, noAr.Id, foraDoAr.Id);
        }

        await using (var context = fixture.CreateDbContext())
        {
            var telas = await new GetTelasQueryHandler(context, Atual(_parceiroA), Identity, TimeProvider.System)
                .Handle(new GetTelasQuery(), CancellationToken.None);

            telas.Items.Should().HaveCount(3);
            telas.Items.Single(t => t.Id == aguardando).Should().BeEquivalentTo(new { Situacao = SituacaoTela.AguardandoPareamento }, o => o.ExcludingMissingMembers());
            telas.Items.Single(t => t.Id == aguardando).CodigoPareamento.Should().MatchRegex("^[A-Z2-9]{8}$");
            telas.Items.Single(t => t.Id == online).Situacao.Should().Be(SituacaoTela.Online);
            telas.Items.Single(t => t.Id == online).VersaoApp.Should().Be("1.0.0");
            telas.Items.Single(t => t.Id == offline).Situacao.Should().Be(SituacaoTela.Offline);

            var dePoisDeAmanha = new RelogioFixo(agora.AddDays(2));
            var expirada = await new GetTelaByIdQueryHandler(context, Atual(_parceiroA), Identity, dePoisDeAmanha)
                .Handle(new GetTelaByIdQuery(aguardando), CancellationToken.None);
            expirada.CodigoPareamento.Should().BeNull();
        }
    }

    private async Task<long> CriarMidiaAsync(long parceiroId, int duracao)
    {
        await using var context = fixture.CreateDbContext();
        var midia = new Midia
        {
            ParceiroId = parceiroId,
            Titulo = "Mídia",
            Tipo = TipoMidia.Video,
            ChaveArmazenamento = ChavesArmazenamento.Gerar(parceiroId, TiposArquivoMidia.Mp4),
            TipoConteudo = TiposArquivoMidia.Mp4,
            TamanhoBytes = 1000,
            Hash = new string('b', 64),
            DuracaoSegundos = duracao,
        };
        context.Midias.Add(midia);
        await context.SaveChangesAsync();
        return midia.Id;
    }

    private async Task<long> CriarPlaylistAsync(long parceiroId, string nome, params long[] midias)
    {
        await using var context = fixture.CreateDbContext();
        var playlist = new Playlist { ParceiroId = parceiroId, Nome = nome };
        playlist.DefinirItens(midias.Select(m => new ItemPlaylistInformado(null, m, DiasSemana.Todos)).ToList());
        context.Playlists.Add(playlist);
        await context.SaveChangesAsync();
        return playlist.Id;
    }

    private static IParceiroAtual Atual(long parceiroId)
    {
        var atual = new Mock<IParceiroAtual>();
        atual.Setup(a => a.ObterAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParceiroDoUsuario(parceiroId, PapelParceiro.Proprietario, "America/Sao_Paulo"));
        return atual.Object;
    }

    private static IIdentityService IdentitySemNomes()
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(i => i.GetUserNamesAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, string>());
        return identity.Object;
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
