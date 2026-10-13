using System.Security.Cryptography;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Commands.CriarMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EnvioMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EstadoMidia;
using Atlas.Application.Business.Indoor.Midias.Queries;
using Atlas.Application.Business.Indoor.Playlists.Commands.DefinirItensPlaylist;
using Atlas.Application.Business.Indoor.Playlists.Commands.EstadoPlaylist;
using Atlas.Application.Business.Indoor.Playlists.Commands.SalvarPlaylist;
using Atlas.Application.Business.Indoor.Playlists.Queries;
using Atlas.Application.Business.Indoor.Telas.Commands.Pareamento;
using Atlas.Application.Business.Indoor.Telas.Commands.SalvarTela;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Regras do painel do parceiro (mídias, playlists, telas) no banco em memória.</summary>
public class PainelIndoorTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly RelogioFixo _relogio = new(Agora);
    private readonly ParceiroAtualFake _parceiroAtual;
    private readonly long _parceiroA;
    private readonly long _parceiroB;

    public PainelIndoorTests()
    {
        var a = new Parceiro { Nome = "Academia", Email = "a@teste.local", CotaArmazenamentoMb = 1 };
        var b = new Parceiro { Nome = "Tapiocaria", Email = "b@teste.local" };
        _context.Parceiros.AddRange(a, b);
        _context.SaveChanges();
        _parceiroA = a.Id;
        _parceiroB = b.Id;
        _parceiroAtual = new ParceiroAtualFake(_parceiroA);
    }

    public void Dispose() => _context.Dispose();

    // ---------- Mídias: envio ----------

    [Fact]
    public async Task PrepararEnvio_DeveGerarChaveDoParceiro()
    {
        var envio = await new PrepararEnvioMidiaCommandHandler(_context, _parceiroAtual, _armazenamento)
            .Handle(new PrepararEnvioMidiaCommand { TipoConteudo = TiposArquivoMidia.Mp4, TamanhoBytes = 1000 }, CancellationToken.None);

        ChavesArmazenamento.Valida(envio.Chave, _parceiroA, TiposArquivoMidia.Mp4).Should().BeTrue();
        envio.UrlEnvio.ToString().Should().EndWith(envio.Chave);
    }

    [Fact]
    public async Task PrepararEnvio_AcimaDaCotaDeveSerRecusado()
    {
        // Cota de 1 MB, com 600 KB já usados.
        await CriarMidiaNoBancoAsync(_parceiroA, tamanho: 600 * 1024);

        var acao = () => new PrepararEnvioMidiaCommandHandler(_context, _parceiroAtual, _armazenamento)
            .Handle(new PrepararEnvioMidiaCommand { TipoConteudo = TiposArquivoMidia.Mp4, TamanhoBytes = 500 * 1024 }, CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("TamanhoBytes");
    }

    [Fact]
    public async Task CriarMidia_DeveGravarOHashETamanhoCalculadosPelaApi()
    {
        var arquivo = Arquivos.Mp4();
        var chave = Enviar(arquivo, TiposArquivoMidia.Mp4);

        var id = await CriarMidia(new CriarMidiaCommand
        {
            Titulo = " Promoção de inverno ",
            Chave = chave,
            TipoConteudo = TiposArquivoMidia.Mp4,
            Hash = Sha256(arquivo).ToUpperInvariant(),
            DuracaoSegundos = 30,
            InicioExibicao = new DateOnly(2026, 11, 1),
        });

        var midia = await _context.Midias.SingleAsync(m => m.Id == id);
        midia.ParceiroId.Should().Be(_parceiroA);
        midia.Titulo.Should().Be("Promoção de inverno");
        midia.Tipo.Should().Be(TipoMidia.Video);
        midia.Hash.Should().Be(Sha256(arquivo));
        midia.TamanhoBytes.Should().Be(arquivo.Length);
        midia.DuracaoSegundos.Should().Be(30);
        _armazenamento.Removidos.Should().BeEmpty();
    }

    [Fact]
    public async Task CriarMidia_ChaveDeOutroParceiroDeveSerRecusadaSemTocarNoArquivo()
    {
        var chave = ChavesArmazenamento.Gerar(_parceiroB, TiposArquivoMidia.Png);
        _armazenamento.Arquivos[chave] = Arquivos.Png();

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Chave");
        _armazenamento.Arquivos.Should().ContainKey(chave, "o arquivo é do outro parceiro e não pode ser apagado por este");
    }

    [Fact]
    public async Task CriarMidia_ConteudoDeOutroTipoDeveSerRecusadoEApagado()
    {
        var chave = Enviar(Arquivos.Png(), TiposArquivoMidia.Mp4); // PNG renomeado para .mp4

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Mp4 });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("TipoConteudo");
        _armazenamento.Removidos.Should().Equal(chave);
        (await _context.Midias.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CriarMidia_HashDivergenteDeveSerRecusadoEApagado()
    {
        var chave = Enviar(Arquivos.Png(), TiposArquivoMidia.Png);

        var acao = () => CriarMidia(new CriarMidiaCommand
        {
            Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png, Hash = new string('a', 64),
        });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Hash");
        _armazenamento.Removidos.Should().Equal(chave);
    }

    [Fact]
    public async Task CriarMidia_ArquivoQueNaoChegouDeveSerRecusado()
    {
        var chave = ChavesArmazenamento.Gerar(_parceiroA, TiposArquivoMidia.Png);

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Should().ContainMatch("*não chegou*");
    }

    [Fact]
    public async Task CriarMidia_ArquivoQueEstouraACotaDeveSerApagado()
    {
        await CriarMidiaNoBancoAsync(_parceiroA, tamanho: 1024 * 1024 - 100);
        var chave = Enviar(Arquivos.Png(500), TiposArquivoMidia.Png);

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        await acao.Should().ThrowAsync<ValidationException>();
        _armazenamento.Removidos.Should().Equal(chave);
    }

    [Fact]
    public async Task CriarMidia_MesmaChaveDuasVezesDeveSerRecusada()
    {
        var chave = Enviar(Arquivos.Png(), TiposArquivoMidia.Png);
        await CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "Y", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        await acao.Should().ThrowAsync<ValidationException>();
        _armazenamento.Removidos.Should().BeEmpty("o arquivo pertence à mídia já cadastrada");
    }

    [Fact]
    public void CriarMidia_ValidadorDeveRecusarPeriodoInvertidoEDuracaoInvalida()
    {
        var resultado = new CriarMidiaCommandValidator().Validate(new CriarMidiaCommand
        {
            Titulo = "",
            Chave = "x",
            TipoConteudo = "video/webm",
            Hash = "abc",
            DuracaoSegundos = 0,
            InicioExibicao = new DateOnly(2026, 11, 30),
            FimExibicao = new DateOnly(2026, 11, 1),
        });

        resultado.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "Titulo", "TipoConteudo", "Hash", "DuracaoSegundos", "FimExibicao");
    }

    // ---------- Mídias: consulta e exclusão ----------

    [Fact]
    public async Task Midias_DeOutroParceiroNaoAparecemNemSeAlteram()
    {
        var midiaB = await CriarMidiaNoBancoAsync(_parceiroB);

        var lista = await new GetMidiasQueryHandler(_context, _parceiroAtual, new IdentityNomesFake())
            .Handle(new GetMidiasQuery(), CancellationToken.None);
        lista.Items.Should().BeEmpty();

        var obter = () => new GetMidiaByIdQueryHandler(_context, _parceiroAtual, new IdentityNomesFake(), _armazenamento)
            .Handle(new GetMidiaByIdQuery(midiaB.Id), CancellationToken.None);
        await obter.Should().ThrowAsync<NotFoundException>();

        var inativar = () => new InativarMidiaCommandHandler(_context, _parceiroAtual)
            .Handle(new InativarMidiaCommand(midiaB.Id, 0), CancellationToken.None);
        await inativar.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ObterMidia_DeveTrazerUrlDeVisualizacao()
    {
        var midia = await CriarMidiaNoBancoAsync(_parceiroA);

        var dto = await new GetMidiaByIdQueryHandler(_context, _parceiroAtual, new IdentityNomesFake(), _armazenamento)
            .Handle(new GetMidiaByIdQuery(midia.Id), CancellationToken.None);

        dto.UrlVisualizacao!.ToString().Should().Contain(midia.ChaveArmazenamento).And.EndWith("s=3600");
    }

    [Fact]
    public async Task ExcluirMidia_EmUsoDeveSerRecusada_ForaDeUsoApagaArquivo()
    {
        var emUso = await CriarMidiaNoBancoAsync(_parceiroA);
        var solta = await CriarMidiaNoBancoAsync(_parceiroA);
        await CriarPlaylistNoBancoAsync(_parceiroA, "Grade", emUso.Id);
        var handler = new ExcluirMidiaCommandHandler(_context, _parceiroAtual, _armazenamento, NullLogger<ExcluirMidiaCommandHandler>.Instance);

        var acao = () => handler.Handle(new ExcluirMidiaCommand(emUso.Id), CancellationToken.None);
        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Id"].Should().ContainMatch("*1 playlist*");

        await handler.Handle(new ExcluirMidiaCommand(solta.Id), CancellationToken.None);
        (await _context.Midias.AnyAsync(m => m.Id == solta.Id)).Should().BeFalse();
        _armazenamento.Removidos.Should().Equal(solta.ChaveArmazenamento);
    }

    // ---------- Playlists ----------

    [Fact]
    public async Task CriarPlaylist_NomeRepetidoNoMesmoParceiroDeveSerRecusado_EmOutroParceiroPode()
    {
        await CriarPlaylistNoBancoAsync(_parceiroB, "Grade principal");
        var handler = new CriarPlaylistCommandHandler(_context, _parceiroAtual);

        await handler.Handle(new CriarPlaylistCommand { Nome = "Grade principal" }, CancellationToken.None);
        var repetida = () => handler.Handle(new CriarPlaylistCommand { Nome = " GRADE PRINCIPAL " }, CancellationToken.None);

        (await repetida.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Nome");
    }

    [Fact]
    public async Task DefinirItens_DeveGravarNaOrdemEContarDuracao()
    {
        var video = await CriarMidiaNoBancoAsync(_parceiroA, duracao: 30);
        var imagem = await CriarMidiaNoBancoAsync(_parceiroA, duracao: 10);
        var playlist = await CriarPlaylistNoBancoAsync(_parceiroA, "Grade");

        await DefinirItens(playlist.Id,
            Item(null, imagem.Id, DiasSemana.DiasUteis),
            Item(null, video.Id, DiasSemana.Todos),
            Item(null, imagem.Id, DiasSemana.FimDeSemana));

        var dto = await new GetPlaylistByIdQueryHandler(_context, _parceiroAtual, new IdentityNomesFake())
            .Handle(new GetPlaylistByIdQuery(playlist.Id), CancellationToken.None);

        dto.Itens!.Select(i => (i.Ordem, i.MidiaId, i.DiasSemana)).Should().Equal(
            (0, imagem.Id, DiasSemana.DiasUteis),
            (1, video.Id, DiasSemana.Todos),
            (2, imagem.Id, DiasSemana.FimDeSemana));
        dto.QuantidadeItens.Should().Be(3);
        dto.DuracaoTotalSegundos.Should().Be(50);
    }

    [Fact]
    public async Task DefinirItens_ComMidiaDeOutroParceiroDeveSerRecusado()
    {
        var midiaB = await CriarMidiaNoBancoAsync(_parceiroB);
        var playlist = await CriarPlaylistNoBancoAsync(_parceiroA, "Grade");

        var acao = () => DefinirItens(playlist.Id, Item(null, midiaB.Id, DiasSemana.Todos));

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Itens");
        (await _context.ItensPlaylist.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DefinirItens_PlaylistDeOutroParceiroDeveDar404()
    {
        var playlistB = await CriarPlaylistNoBancoAsync(_parceiroB, "Grade B");

        var acao = () => DefinirItens(playlistB.Id);

        await acao.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExcluirPlaylist_EmUsoPorTelaDeveSerRecusada()
    {
        var playlist = await CriarPlaylistNoBancoAsync(_parceiroA, "Grade");
        _context.Telas.Add(new Tela { ParceiroId = _parceiroA, Nome = "Recepção", PlaylistId = playlist.Id });
        await _context.SaveChangesAsync();

        var acao = () => new ExcluirPlaylistCommandHandler(_context, _parceiroAtual)
            .Handle(new ExcluirPlaylistCommand(playlist.Id), CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Id"].Should().ContainMatch("*1 tela*");
    }

    // ---------- Telas ----------

    [Fact]
    public async Task CriarTela_ComPlaylistDeOutroParceiroDeveSerRecusada()
    {
        var playlistB = await CriarPlaylistNoBancoAsync(_parceiroB, "Grade B");

        var acao = () => new CriarTelaCommandHandler(_context, _parceiroAtual)
            .Handle(new CriarTelaCommand { Nome = "Recepção", PlaylistId = playlistB.Id }, CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("PlaylistId");
    }

    [Fact]
    public async Task GerarCodigo_DeveSerLegivelEValerVinteEQuatroHoras()
    {
        var id = await new CriarTelaCommandHandler(_context, _parceiroAtual)
            .Handle(new CriarTelaCommand { Nome = "Recepção" }, CancellationToken.None);

        var codigo = await new GerarCodigoPareamentoCommandHandler(_context, _parceiroAtual, _relogio)
            .Handle(new GerarCodigoPareamentoCommand(id, 0), CancellationToken.None);

        codigo.Codigo.Should().MatchRegex("^[A-HJ-NP-Z2-9]{8}$");
        codigo.ExpiraEm.Should().Be(Agora.AddHours(24));

        var tela = await Telas().Handle(new GetTelaByIdQuery(id), CancellationToken.None);
        tela.CodigoPareamento.Should().Be(codigo.Codigo);
        tela.Situacao.Should().Be(SituacaoTela.AguardandoPareamento);

        _relogio.Agora = Agora.AddHours(25);
        (await Telas().Handle(new GetTelaByIdQuery(id), CancellationToken.None)).CodigoPareamento.Should().BeNull("o código expirou");
    }

    [Fact]
    public async Task GerarCodigo_TelaInativaDeveSerRecusada()
    {
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção", Ativo = false };
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        var acao = () => new GerarCodigoPareamentoCommandHandler(_context, _parceiroAtual, _relogio)
            .Handle(new GerarCodigoPareamentoCommand(tela.Id, 0), CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Situacao_DeveSeguirASincronizacaoEODesvinculo()
    {
        var tela = new Tela { ParceiroId = _parceiroA, Nome = "Recepção" };
        tela.Parear("tv-1", Agora.AddHours(-2));
        tela.Estado = new EstadoTela();
        tela.Estado.RegistrarSincronizacao(Agora.AddMinutes(-5), "1.0.0", "v", 3);
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        (await Telas().Handle(new GetTelaByIdQuery(tela.Id), CancellationToken.None)).Situacao.Should().Be(SituacaoTela.Online);

        _relogio.Agora = Agora.AddMinutes(20);
        (await Telas().Handle(new GetTelaByIdQuery(tela.Id), CancellationToken.None)).Situacao.Should().Be(SituacaoTela.Offline);

        await new DesvincularTelaCommandHandler(_context, _parceiroAtual).Handle(new DesvincularTelaCommand(tela.Id, 0), CancellationToken.None);
        (await Telas().Handle(new GetTelaByIdQuery(tela.Id), CancellationToken.None)).Situacao.Should().Be(SituacaoTela.AguardandoPareamento);

        var deNovo = () => new DesvincularTelaCommandHandler(_context, _parceiroAtual).Handle(new DesvincularTelaCommand(tela.Id, 0), CancellationToken.None);
        await deNovo.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Telas_DeOutroParceiroNaoAparecem()
    {
        _context.Telas.Add(new Tela { ParceiroId = _parceiroB, Nome = "TV da tapiocaria" });
        await _context.SaveChangesAsync();

        var lista = await new GetTelasQueryHandler(_context, _parceiroAtual, new IdentityNomesFake(), _relogio)
            .Handle(new GetTelasQuery(), CancellationToken.None);

        lista.Items.Should().BeEmpty();
    }

    // ---------- Apoio ----------

    private GetTelaByIdQueryHandler Telas() => new(_context, _parceiroAtual, new IdentityNomesFake(), _relogio);

    private Task<long> CriarMidia(CriarMidiaCommand comando) =>
        new CriarMidiaCommandHandler(_context, _parceiroAtual, _armazenamento, NullLogger<CriarMidiaCommandHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    private Task<uint> DefinirItens(long playlistId, params DefinirItensPlaylistCommand.ItemInformado[] itens) =>
        new DefinirItensPlaylistCommandHandler(_context, _parceiroAtual)
            .Handle(new DefinirItensPlaylistCommand { Id = playlistId, Itens = itens, Version = 0 }, CancellationToken.None);

    private static DefinirItensPlaylistCommand.ItemInformado Item(long? id, long midiaId, DiasSemana dias) => new(id, midiaId, dias);

    private string Enviar(byte[] arquivo, string tipo)
    {
        var chave = ChavesArmazenamento.Gerar(_parceiroA, tipo);
        _armazenamento.Arquivos[chave] = arquivo;
        return chave;
    }

    private async Task<Midia> CriarMidiaNoBancoAsync(long parceiroId, long tamanho = 1000, int duracao = 15)
    {
        var midia = new Midia
        {
            ParceiroId = parceiroId,
            Titulo = "Mídia",
            Tipo = TipoMidia.Video,
            ChaveArmazenamento = ChavesArmazenamento.Gerar(parceiroId, TiposArquivoMidia.Mp4),
            TipoConteudo = TiposArquivoMidia.Mp4,
            TamanhoBytes = tamanho,
            Hash = new string('a', 64),
            DuracaoSegundos = duracao,
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();
        return midia;
    }

    private async Task<Playlist> CriarPlaylistNoBancoAsync(long parceiroId, string nome, params long[] midias)
    {
        var playlist = new Playlist { ParceiroId = parceiroId, Nome = nome };
        playlist.DefinirItens(midias.Select(m => new ItemPlaylistInformado(null, m, DiasSemana.Todos)).ToList());
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist;
    }

    private static string Sha256(byte[] dados) => Convert.ToHexStringLower(SHA256.HashData(dados));
}
