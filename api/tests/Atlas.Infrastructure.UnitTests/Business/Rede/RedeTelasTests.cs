using Atlas.Application.Business.Rede.Telas.Commands.EstadoTela;
using Atlas.Application.Business.Rede.Telas.Commands.Pareamento;
using Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;
using Atlas.Application.Business.Rede.Telas.Queries;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.UnitTests.Business.Indoor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace Atlas.Infrastructure.UnitTests.Business.Rede;

/// <summary>Telas na visão da operadora, dona do hardware: cadastro, parceiro e pareamento.</summary>
public class RedeTelasTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly RelogioFixo _relogio = new(Agora);
    private readonly ConfiguracaoIndoorFake _configuracao = new();
    private readonly long _parceiroA;
    private readonly long _parceiroB;

    public RedeTelasTests()
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
    public async Task Criar_DeveGravarOParceiroEAIdentificacao()
    {
        var id = await Criar(new CriarTelaCommand
        {
            ParceiroId = _parceiroA,
            Nome = " Recepção ",
            Localizacao = " Térreo ",
            IdentificacaoAparelho = " 00:1A:2B:3C:4D:5E ",
        });

        var tela = await Obter(id);
        tela.ParceiroId.Should().Be(_parceiroA);
        tela.ParceiroNome.Should().Be("Academia");
        tela.Nome.Should().Be("Recepção");
        tela.Localizacao.Should().Be("Térreo");
        tela.IdentificacaoAparelho.Should().Be("00:1A:2B:3C:4D:5E");
        tela.PlaylistId.Should().BeNull("sem playlist informada");
        tela.Situacao.Should().Be(SituacaoTela.AguardandoPareamento);
    }

    [Fact]
    public async Task Criar_ParceiroInexistenteDeveSerRecusado()
    {
        var acao = () => Criar(new CriarTelaCommand { ParceiroId = 999, Nome = "Recepção" });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("ParceiroId");
    }

    [Fact]
    public async Task Criar_NomeRepetidoNoMesmoParceiroDeveSerRecusado_EmOutroParceiroPode()
    {
        await Criar(new CriarTelaCommand { ParceiroId = _parceiroA, Nome = "Recepção" });

        var repetido = () => Criar(new CriarTelaCommand { ParceiroId = _parceiroA, Nome = "recepção" });
        (await repetido.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Nome");

        await Criar(new CriarTelaCommand { ParceiroId = _parceiroB, Nome = "Recepção" });
        (await _context.Telas.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Atualizar_TrocarDeHostMantemPlaylistETV()
    {
        // A playlist é da rede e a TV é da operadora: mudar o host não mexe nelas.
        var playlist = new Playlist { Nome = "Grade" };
        _context.Playlists.Add(playlist);
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção", Playlist = playlist };
        tela.Parear("tv-1", Agora.AddHours(-1));
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();
        var credencialAntes = tela.VersaoCredencial;

        await new AtualizarTelaCommandHandler(_context).Handle(new AtualizarTelaCommand
        {
            Id = tela.Id,
            ParceiroId = _parceiroB,
            Nome = "Balcão",
            PlaylistId = playlist.Id,
            Version = 0,
        }, CancellationToken.None);

        var salva = await _context.Telas.AsNoTracking().SingleAsync();
        salva.ParceiroId.Should().Be(_parceiroB);
        salva.PlaylistId.Should().Be(playlist.Id);
        salva.DispositivoId.Should().Be("tv-1");
        salva.VersaoCredencial.Should().Be(credencialAntes);
    }

    [Fact]
    public async Task CriarEAtualizar_EscolhemAPlaylist_InexistenteERecusada()
    {
        var playlist = new Playlist { Nome = "Grade" };
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();

        var id = await Criar(new CriarTelaCommand { ParceiroId = _parceiroA, Nome = "Recepção", PlaylistId = playlist.Id });
        (await Obter(id)).PlaylistNome.Should().Be("Grade");

        var acao = () => Criar(new CriarTelaCommand { ParceiroId = _parceiroA, Nome = "Outra", PlaylistId = 999 });
        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("PlaylistId");
    }

    [Fact]
    public async Task Atualizar_MesmoParceiroMantemPlaylistETV()
    {
        var playlist = new Playlist { Nome = "Grade" };
        _context.Playlists.Add(playlist);
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção", Playlist = playlist };
        tela.Parear("tv-1", Agora.AddHours(-1));
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        await new AtualizarTelaCommandHandler(_context).Handle(new AtualizarTelaCommand
        {
            Id = tela.Id,
            ParceiroId = _parceiroA,
            Nome = "Recepção principal",
            IdentificacaoAparelho = "SN-123",
            PlaylistId = playlist.Id,
            Version = 0,
        }, CancellationToken.None);

        var salva = await _context.Telas.AsNoTracking().SingleAsync();
        salva.Nome.Should().Be("Recepção principal");
        salva.IdentificacaoAparelho.Should().Be("SN-123");
        salva.PlaylistId.Should().Be(playlist.Id);
        salva.DispositivoId.Should().Be("tv-1");
    }

    [Fact]
    public async Task GerarCodigo_DeveSerLegivelEValerVinteEQuatroHoras()
    {
        var id = await Criar(new CriarTelaCommand { ParceiroId = _parceiroA, Nome = "Recepção" });

        var codigo = await new GerarCodigoPareamentoCommandHandler(_context, _relogio)
            .Handle(new GerarCodigoPareamentoCommand(id, 0), CancellationToken.None);

        codigo.Codigo.Should().MatchRegex("^[A-HJ-NP-Z2-9]{8}$");
        codigo.ExpiraEm.Should().Be(Agora.AddHours(24));
        (await Obter(id)).CodigoPareamento.Should().Be(codigo.Codigo, "a operadora vê o código pendente");

        _relogio.Agora = Agora.AddHours(25);
        (await Obter(id)).CodigoPareamento.Should().BeNull("o código expirou");
    }

    [Fact]
    public async Task GerarCodigo_TelaInativaDeveSerRecusada()
    {
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção", Ativo = false };
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        var acao = () => new GerarCodigoPareamentoCommandHandler(_context, _relogio)
            .Handle(new GerarCodigoPareamentoCommand(tela.Id, 0), CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Desvincular_VoltaAAguardarPareamento()
    {
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção" };
        tela.Parear("tv-1", Agora.AddHours(-2));
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        await new DesvincularTelaCommandHandler(_context).Handle(new DesvincularTelaCommand(tela.Id, 0), CancellationToken.None);
        (await Obter(tela.Id)).Situacao.Should().Be(SituacaoTela.AguardandoPareamento);

        var deNovo = () => new DesvincularTelaCommandHandler(_context).Handle(new DesvincularTelaCommand(tela.Id, 0), CancellationToken.None);
        await deNovo.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task InativarEExcluir_FuncionamEmTelaDeQualquerParceiro()
    {
        var tela = new Tela { ParceiroId = _parceiroB, Nome = "Balcão" };
        tela.GerarCodigoPareamento("ABCD2345", Agora);
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        await new InativarTelaCommandHandler(_context).Handle(new InativarTelaCommand(tela.Id, 0), CancellationToken.None);
        var inativa = await _context.Telas.AsNoTracking().SingleAsync();
        inativa.Ativo.Should().BeFalse();
        inativa.CodigoPareamento.Should().BeNull();

        await new ExcluirTelaCommandHandler(_context).Handle(new ExcluirTelaCommand(tela.Id), CancellationToken.None);
        (await _context.Telas.AnyAsync()).Should().BeFalse();

        var inexistente = () => new ExcluirTelaCommandHandler(_context).Handle(new ExcluirTelaCommand(tela.Id), CancellationToken.None);
        await inexistente.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Listar_TrazTodosOsParceirosEFiltraPorParceiroEBusca()
    {
        _context.Telas.AddRange(
            new Tela { ParceiroId = _parceiroA, Nome = "Recepção" },
            new Tela { ParceiroId = _parceiroA, Nome = "Musculação", IdentificacaoAparelho = "SN-777" },
            new Tela { ParceiroId = _parceiroB, Nome = "Balcão" });
        await _context.SaveChangesAsync();

        var todas = await Listar(new GetTelasRedeQuery());
        todas.Items.Select(t => (t.ParceiroNome, t.Nome)).Should().Equal(
            ("Academia", "Musculação"), ("Academia", "Recepção"), ("Tapiocaria", "Balcão"));

        (await Listar(new GetTelasRedeQuery { ParceiroId = _parceiroB })).Items.Should().ContainSingle(t => t.Nome == "Balcão");
        (await Listar(new GetTelasRedeQuery { SearchTerm = "tapio" })).Items.Should().ContainSingle(t => t.Nome == "Balcão");
        (await Listar(new GetTelasRedeQuery { SearchTerm = "sn-777" })).Items.Should().ContainSingle(t => t.Nome == "Musculação");
    }

    // ---------- Apoio ----------

    private Task<long> Criar(CriarTelaCommand comando) =>
        new CriarTelaCommandHandler(_context).Handle(comando, CancellationToken.None);

    private Task<Application.Business.Indoor.Telas.Dtos.TelaDto> Obter(long id) =>
        new GetTelaRedeByIdQueryHandler(_context, new IdentityNomesFake(), _configuracao, _relogio)
            .Handle(new GetTelaRedeByIdQuery(id), CancellationToken.None);

    private Task<Application.Common.Models.Pagination.PaginatedList<Application.Business.Indoor.Telas.Dtos.TelaDto>> Listar(GetTelasRedeQuery query) =>
        new GetTelasRedeQueryHandler(_context, new IdentityNomesFake(), _configuracao, _relogio).Handle(query, CancellationToken.None);
}
