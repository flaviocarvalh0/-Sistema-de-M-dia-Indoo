using Atlas.Application.Business.Rede.Playlists.Commands.DefinirItensPlaylist;
using Atlas.Application.Business.Rede.Playlists.Commands.EstadoPlaylist;
using Atlas.Application.Business.Rede.Playlists.Commands.SalvarPlaylist;
using Atlas.Application.Business.Rede.Playlists.Queries;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.UnitTests.Business.Indoor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Rede;

/// <summary>Playlists da rede: a operadora monta as grades com as mídias dos anunciantes.</summary>
public class RedePlaylistsTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly long _parceiroA;
    private readonly long _parceiroB;

    public RedePlaylistsTests()
    {
        var a = new Parceiro { Nome = "Academia", Email = "a@teste.local" };
        var b = new Parceiro { Nome = "Tapiocaria", Email = "b@teste.local" };
        _context.Parceiros.AddRange(a, b);
        _context.SaveChanges();
        _parceiroA = a.Id;
        _parceiroB = b.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CriarPlaylist_NomeEUnicoNaRede()
    {
        var handler = new CriarPlaylistCommandHandler(_context);
        await handler.Handle(new CriarPlaylistCommand { Nome = "Grade principal" }, CancellationToken.None);

        var repetida = () => handler.Handle(new CriarPlaylistCommand { Nome = " GRADE PRINCIPAL " }, CancellationToken.None);

        (await repetida.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Nome");
    }

    [Fact]
    public async Task DefinirItens_MisturaAnunciantesRepeteMidiaEMostraODono()
    {
        var a = await Midia(_parceiroA, "Promo academia", duracao: 30);
        var b = await Midia(_parceiroB, "Tapioca do dia", duracao: 10);
        var playlist = await Playlist("Padaria");

        await DefinirItens(playlist.Id, Item(null, a.Id), Item(null, b.Id), Item(null, a.Id, DiasSemana.DiasUteis));

        var dto = await new GetPlaylistByIdQueryHandler(_context, new IdentityNomesFake())
            .Handle(new GetPlaylistByIdQuery(playlist.Id), CancellationToken.None);

        dto.Itens!.Select(i => (i.Ordem, i.Titulo, i.ParceiroNome)).Should().Equal(
            (0, "Promo academia", "Academia"),
            (1, "Tapioca do dia", "Tapiocaria"),
            (2, "Promo academia", "Academia"));
        dto.DuracaoTotalSegundos.Should().Be(70);
    }

    [Fact]
    public async Task DefinirItens_MidiaInativaNaoEntra_MasItemQueJaEstavaFica()
    {
        var a = await Midia(_parceiroA, "Promo");
        var playlist = await Playlist("Padaria");
        await DefinirItens(playlist.Id, Item(null, a.Id));

        a.Ativo = false;
        await _context.SaveChangesAsync();
        var itemId = (await _context.ItensPlaylist.SingleAsync()).Id;

        // Regravar a grade com o item que já estava passa.
        await DefinirItens(playlist.Id, Item(itemId, a.Id, DiasSemana.FimDeSemana));

        // Pôr a mídia inativa de novo (item novo) não.
        var acao = () => DefinirItens(playlist.Id, Item(itemId, a.Id), Item(null, a.Id));
        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Itens"].Should().ContainMatch("*ativas*");
    }

    [Fact]
    public async Task SalvarPlaylist_GuardaAComposicaoDaFila()
    {
        var id = await new CriarPlaylistCommandHandler(_context).Handle(new CriarPlaylistCommand
        {
            Nome = "Com notícias", NoticiaACadaAnuncios = 3, DuracaoNoticiaSegundos = 12, EmbaralharAnuncios = true, ExibirClima = false,
        }, CancellationToken.None);

        var dto = await new GetPlaylistByIdQueryHandler(_context, new IdentityNomesFake())
            .Handle(new GetPlaylistByIdQuery(id), CancellationToken.None);

        dto.Should().BeEquivalentTo(new
        {
            NoticiaACadaAnuncios = 3, DuracaoNoticiaSegundos = 12, EmbaralharAnuncios = true, ExibirRelogio = true, ExibirClima = false,
        });

        // Sem os campos novos (cliente antigo): relógio e clima ligados, sem notícias.
        var padrao = await new CriarPlaylistCommandHandler(_context).Handle(new CriarPlaylistCommand { Nome = "Padrão" }, CancellationToken.None);
        (await _context.Playlists.SingleAsync(p => p.Id == padrao)).Should().Match<Playlist>(p =>
            p.NoticiaACadaAnuncios == 0 && p.DuracaoNoticiaSegundos == 10 && !p.EmbaralharAnuncios && p.ExibirRelogio && p.ExibirClima);
    }

    [Theory]
    [InlineData(0, 10, true)]
    [InlineData(20, 60, true)]
    [InlineData(21, 10, false)]
    [InlineData(-1, 10, false)]
    [InlineData(3, 4, false)]
    [InlineData(3, 61, false)]
    public void ValidadorPlaylist_LimitesDaComposicao(int aCada, int duracao, bool valido)
    {
        new CriarPlaylistCommandValidator()
            .Validate(new CriarPlaylistCommand { Nome = "A", NoticiaACadaAnuncios = aCada, DuracaoNoticiaSegundos = duracao })
            .IsValid.Should().Be(valido);
    }

    [Fact]
    public async Task DefinirItens_GuardaAPosicaoFixa()
    {
        var a = await Midia(_parceiroA, "Da casa");
        var b = await Midia(_parceiroB, "Anúncio");
        var playlist = await Playlist("Padaria");

        await DefinirItens(playlist.Id, Item(null, a.Id) with { PosicaoFixa = true }, Item(null, b.Id));

        var dto = await new GetPlaylistByIdQueryHandler(_context, new IdentityNomesFake())
            .Handle(new GetPlaylistByIdQuery(playlist.Id), CancellationToken.None);
        dto.Itens!.Select(i => (i.Titulo, i.PosicaoFixa)).Should().Equal(("Da casa", true), ("Anúncio", false));
    }

    [Fact]
    public async Task DefinirItens_MidiaInexistenteDeveSerRecusada()
    {
        var playlist = await Playlist("Padaria");

        var acao = () => DefinirItens(playlist.Id, Item(null, 999));

        await acao.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ExcluirPlaylist_EmUsoPorTelaDeveSerRecusada()
    {
        var playlist = await Playlist("Padaria");
        _context.Telas.Add(new Tela { ParceiroId = _parceiroB, Nome = "TV", PlaylistId = playlist.Id });
        await _context.SaveChangesAsync();

        var acao = () => new ExcluirPlaylistCommandHandler(_context).Handle(new ExcluirPlaylistCommand(playlist.Id), CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Id"].Should().ContainMatch("*1 tela*");
    }

    [Fact]
    public async Task Listar_TrazTodasAsPlaylistsDaRede()
    {
        await Playlist("Padaria");
        await Playlist("Academia");

        var lista = await new GetPlaylistsQueryHandler(_context, new IdentityNomesFake())
            .Handle(new GetPlaylistsQuery(), CancellationToken.None);

        lista.Items.Select(p => p.Nome).Should().Equal("Academia", "Padaria");
    }

    // ---------- Apoio ----------

    private Task<uint> DefinirItens(long playlistId, params DefinirItensPlaylistCommand.ItemInformado[] itens) =>
        new DefinirItensPlaylistCommandHandler(_context)
            .Handle(new DefinirItensPlaylistCommand { Id = playlistId, Itens = itens, Version = 0 }, CancellationToken.None);

    private static DefinirItensPlaylistCommand.ItemInformado Item(long? id, long midiaId, DiasSemana dias = DiasSemana.Todos) => new(id, midiaId, dias);

    private async Task<Playlist> Playlist(string nome)
    {
        var playlist = new Playlist { Nome = nome };
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist;
    }

    private async Task<Midia> Midia(long parceiroId, string titulo, int duracao = 15)
    {
        var midia = new Midia
        {
            ParceiroId = parceiroId,
            Titulo = titulo,
            Tipo = TipoMidia.Video,
            ChaveArmazenamento = $"parceiros/{parceiroId}/midias/{Guid.NewGuid():N}.mp4",
            TipoConteudo = "video/mp4",
            TamanhoBytes = 100,
            Hash = new string('a', 64),
            DuracaoSegundos = duracao,
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();
        return midia;
    }
}
