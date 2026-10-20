using Atlas.Domain.Business.Parceiros.Entities;
using System.Security.Cryptography;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Midias.Commands.CriarMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EnvioMidia;
using Atlas.Application.Business.Indoor.Midias.Commands.EstadoMidia;
using Atlas.Application.Business.Indoor.Midias.Queries;
using Atlas.Application.Business.Indoor.Midias.Commands.SubstituirArquivo;
using Atlas.Application.Business.Indoor.Telas.Queries;
using Atlas.Application.Business.Rede.Midias.Commands;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Regras do painel do cliente (mídias, plano, onde os anúncios passam) no banco em memória.</summary>
public class PainelIndoorTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly RelogioFixo _relogio = new(Agora);
    private readonly ClienteAtualFake _clienteAtual;
    private readonly EmailsFake _emails = new();
    private readonly long _clienteA;
    private readonly long _clienteB;

    public PainelIndoorTests()
    {
        var a = new Cliente { Nome = "Academia", Email = "a@teste.local", CotaArmazenamentoMb = 1 };
        var b = new Cliente { Nome = "Tapiocaria", Email = "b@teste.local" };
        _context.Clientes.AddRange(a, b);
        _context.SaveChanges();
        _clienteA = a.Id;
        _clienteB = b.Id;
        _clienteAtual = new ClienteAtualFake(_clienteA);
    }

    public void Dispose() => _context.Dispose();

    // ---------- Mídias: envio ----------

    [Fact]
    public async Task PrepararEnvio_DeveGerarChaveDoCliente()
    {
        var envio = await new PrepararEnvioMidiaCommandHandler(_context, _clienteAtual, _armazenamento)
            .Handle(new PrepararEnvioMidiaCommand { TipoConteudo = TiposArquivoMidia.Mp4, TamanhoBytes = 1000 }, CancellationToken.None);

        ChavesArmazenamento.Valida(envio.Chave, _clienteA, TiposArquivoMidia.Mp4).Should().BeTrue();
        envio.UrlEnvio.ToString().Should().EndWith(envio.Chave);
    }

    [Fact]
    public async Task PrepararEnvio_AcimaDaCotaDeveSerRecusado()
    {
        // Cota de 1 MB, com 600 KB já usados.
        await CriarMidiaNoBancoAsync(_clienteA, tamanho: 600 * 1024);

        var acao = () => new PrepararEnvioMidiaCommandHandler(_context, _clienteAtual, _armazenamento)
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
        midia.ClienteId.Should().Be(_clienteA);
        midia.Titulo.Should().Be("Promoção de inverno");
        midia.Tipo.Should().Be(TipoMidia.Video);
        midia.Hash.Should().Be(Sha256(arquivo));
        midia.TamanhoBytes.Should().Be(arquivo.Length);
        midia.DuracaoSegundos.Should().Be(30);
        _armazenamento.Removidos.Should().BeEmpty();
    }

    [Fact]
    public async Task CriarMidia_ChaveDeOutroClienteDeveSerRecusadaSemTocarNoArquivo()
    {
        var chave = ChavesArmazenamento.Gerar(_clienteB, TiposArquivoMidia.Png);
        _armazenamento.Arquivos[chave] = Arquivos.Png();

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Chave");
        _armazenamento.Arquivos.Should().ContainKey(chave, "o arquivo é do outro cliente e não pode ser apagado por este");
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
        var chave = ChavesArmazenamento.Gerar(_clienteA, TiposArquivoMidia.Png);

        var acao = () => CriarMidia(new CriarMidiaCommand { Titulo = "X", Chave = chave, TipoConteudo = TiposArquivoMidia.Png });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Should().ContainMatch("*não chegou*");
    }

    [Fact]
    public async Task CriarMidia_ArquivoQueEstouraACotaDeveSerApagado()
    {
        await CriarMidiaNoBancoAsync(_clienteA, tamanho: 1024 * 1024 - 100);
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
    public async Task Midias_DeOutroClienteNaoAparecemNemSeAlteram()
    {
        var midiaB = await CriarMidiaNoBancoAsync(_clienteB);

        var lista = await new GetMidiasQueryHandler(_context, _clienteAtual, new IdentityNomesFake(), new ConfiguracaoIndoorFake())
            .Handle(new GetMidiasQuery(), CancellationToken.None);
        lista.Items.Should().BeEmpty();

        var obter = () => new GetMidiaByIdQueryHandler(_context, _clienteAtual, new IdentityNomesFake(), _armazenamento, new ConfiguracaoIndoorFake())
            .Handle(new GetMidiaByIdQuery(midiaB.Id), CancellationToken.None);
        await obter.Should().ThrowAsync<NotFoundException>();

        var inativar = () => new InativarMidiaCommandHandler(_context, _clienteAtual)
            .Handle(new InativarMidiaCommand(midiaB.Id, 0), CancellationToken.None);
        await inativar.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ObterMidia_DeveTrazerUrlDeVisualizacao()
    {
        var midia = await CriarMidiaNoBancoAsync(_clienteA);

        var dto = await new GetMidiaByIdQueryHandler(_context, _clienteAtual, new IdentityNomesFake(), _armazenamento, new ConfiguracaoIndoorFake())
            .Handle(new GetMidiaByIdQuery(midia.Id), CancellationToken.None);

        dto.UrlVisualizacao!.ToString().Should().Contain(midia.ChaveArmazenamento).And.EndWith("s=3600");
    }

    [Fact]
    public async Task ExcluirMidia_EmUsoDeveSerRecusada_ForaDeUsoApagaArquivo()
    {
        var emUso = await CriarMidiaNoBancoAsync(_clienteA);
        var solta = await CriarMidiaNoBancoAsync(_clienteA);
        await CriarPlaylistNoBancoAsync("Grade", emUso.Id);
        var handler = new ExcluirMidiaCommandHandler(_context, _clienteAtual, _armazenamento, NullLogger<ExcluirMidiaCommandHandler>.Instance);

        var acao = () => handler.Handle(new ExcluirMidiaCommand(emUso.Id), CancellationToken.None);
        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Id"].Should().ContainMatch("*1 playlist*");

        await handler.Handle(new ExcluirMidiaCommand(solta.Id), CancellationToken.None);
        (await _context.Midias.AnyAsync(m => m.Id == solta.Id)).Should().BeFalse();
        _armazenamento.Removidos.Should().Equal(solta.ChaveArmazenamento);
    }

    // ---------- Plano: limite de mídias ativas ----------

    [Fact]
    public async Task CriarMidia_DentroDoPlanoNasceAtiva_AcimaNasceInativa()
    {
        var primeira = await CriarMidia(NovaMidia(Arquivos.Png(), TiposArquivoMidia.Png));
        var segunda = await CriarMidia(NovaMidia(Arquivos.Png(), TiposArquivoMidia.Png));

        (await _context.Midias.SingleAsync(m => m.Id == primeira)).Ativo.Should().BeTrue();
        (await _context.Midias.SingleAsync(m => m.Id == segunda)).Ativo.Should().BeFalse("o plano padrão permite 1 ativa");
    }

    [Fact]
    public async Task AtivarMidia_AlemDoPlanoDeveDar400()
    {
        await CriarMidiaNoBancoAsync(_clienteA);
        var outra = await CriarMidiaNoBancoAsync(_clienteA, ativa: false);

        var ativar = () => new AtivarMidiaCommandHandler(_context, _clienteAtual)
            .Handle(new AtivarMidiaCommand(outra.Id, 0), CancellationToken.None);

        (await ativar.Should().ThrowAsync<ValidationException>()).Which.Errors["Ativo"]
            .Should().ContainMatch("*1 mídia ativa*");
    }

    [Fact]
    public async Task AtivarMidia_ComPlanoMaiorOuDepoisDeInativarOutraPassa()
    {
        var a = await CriarMidiaNoBancoAsync(_clienteA);
        var b = await CriarMidiaNoBancoAsync(_clienteA, ativa: false);

        await new InativarMidiaCommandHandler(_context, _clienteAtual).Handle(new InativarMidiaCommand(a.Id, 0), CancellationToken.None);
        await new AtivarMidiaCommandHandler(_context, _clienteAtual).Handle(new AtivarMidiaCommand(b.Id, 0), CancellationToken.None);

        (await _context.Clientes.SingleAsync(p => p.Id == _clienteA)).LimiteMidiasAtivas = 2;
        await _context.SaveChangesAsync();
        await new AtivarMidiaCommandHandler(_context, _clienteAtual).Handle(new AtivarMidiaCommand(a.Id, 0), CancellationToken.None);

        (await _context.Midias.CountAsync(m => m.ClienteId == _clienteA && m.Ativo)).Should().Be(2);
    }

    [Fact]
    public async Task Plano_DireitoAdquiridoNaoInativaNada_SoBarraANovaAtivacao()
    {
        // Três ativas de antes do limite existir.
        await CriarMidiaNoBancoAsync(_clienteA);
        await CriarMidiaNoBancoAsync(_clienteA);
        var ativaDeAntes = await CriarMidiaNoBancoAsync(_clienteA);
        var nova = await CriarMidiaNoBancoAsync(_clienteA, ativa: false);

        // Reativar uma que já está ativa não conta como mais uma.
        await new AtivarMidiaCommandHandler(_context, _clienteAtual).Handle(new AtivarMidiaCommand(ativaDeAntes.Id, 0), CancellationToken.None);

        var ativar = () => new AtivarMidiaCommandHandler(_context, _clienteAtual).Handle(new AtivarMidiaCommand(nova.Id, 0), CancellationToken.None);
        await ativar.Should().ThrowAsync<ValidationException>();
        (await _context.Midias.CountAsync(m => m.ClienteId == _clienteA && m.Ativo)).Should().Be(3);

        var uso = await new GetUsoArmazenamentoQueryHandler(_context, _clienteAtual).Handle(new GetUsoArmazenamentoQuery(), CancellationToken.None);
        uso.MidiasAtivas.Should().Be(3);
        uso.LimiteMidiasAtivas.Should().Be(1);
    }

    // ---------- Substituir arquivo ----------

    [Fact]
    public async Task SubstituirArquivo_ForaDasGradesTrocaNaHoraEApagaOAntigo()
    {
        var id = await CriarMidia(NovaMidia(Arquivos.Mp4(), TiposArquivoMidia.Mp4));
        var chaveAntiga = (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento;

        var novo = Arquivos.Png();
        var chaveNova = Enviar(novo, TiposArquivoMidia.Png);
        var resultado = await Substituir(new SubstituirArquivoMidiaCommand
        {
            Id = id, Chave = chaveNova, TipoConteudo = TiposArquivoMidia.Png, Hash = Sha256(novo), DuracaoSegundos = 12, Version = 0,
        });

        resultado.AguardandoAprovacao.Should().BeFalse();
        var midia = await _context.Midias.AsNoTracking().SingleAsync(m => m.Id == id);
        midia.ChaveArmazenamento.Should().Be(chaveNova);
        midia.Hash.Should().Be(Sha256(novo));
        midia.Tipo.Should().Be(TipoMidia.Imagem);
        midia.DuracaoSegundos.Should().Be(12);
        midia.PendenteChave.Should().BeNull();
        _armazenamento.Removidos.Should().Equal(chaveAntiga);
        _emails.Enfileirados.Should().BeEmpty("fora das grades não há o que aprovar");
    }

    [Fact]
    public async Task SubstituirArquivo_EmGradeFicaPendente_AsTvsSeguemComOAtualAteAprovar()
    {
        var antigo = Arquivos.Mp4();
        var id = await CriarMidia(NovaMidia(antigo, TiposArquivoMidia.Mp4));
        var playlist = await CriarPlaylistNoBancoAsync("Grade", id);
        var chaveAntiga = (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento;

        var novo = Arquivos.Png();
        var chaveNova = Enviar(novo, TiposArquivoMidia.Png);
        var resultado = await Substituir(new SubstituirArquivoMidiaCommand
        {
            Id = id, Chave = chaveNova, TipoConteudo = TiposArquivoMidia.Png, Hash = Sha256(novo), DuracaoSegundos = 12, Version = 0,
        });

        resultado.AguardandoAprovacao.Should().BeTrue();
        var pendente = await _context.Midias.AsNoTracking().SingleAsync(m => m.Id == id);
        pendente.ChaveArmazenamento.Should().Be(chaveAntiga, "a TV segue com o arquivo aprovado");
        pendente.Hash.Should().Be(Sha256(antigo));
        pendente.PendenteChave.Should().Be(chaveNova);
        pendente.PendenteEnviadoEm.Should().Be(Agora);
        _armazenamento.Removidos.Should().BeEmpty();
        _emails.Enfileirados.Single().Recipients.Single().Email.Should().Be("operadora@rede.test");

        // A operadora aprova: o novo vale, o antigo sai, o anunciante é avisado.
        await new AprovarTrocaArquivoCommandHandler(_context, _armazenamento, _emails, new ConfiguracaoIndoorFake(),
                NullLogger<AprovarTrocaArquivoCommandHandler>.Instance)
            .Handle(new AprovarTrocaArquivoCommand(id, 0), CancellationToken.None);

        var midia = await _context.Midias.AsNoTracking().SingleAsync(m => m.Id == id);
        midia.ChaveArmazenamento.Should().Be(chaveNova);
        midia.Hash.Should().Be(Sha256(novo));
        midia.TamanhoBytes.Should().Be(novo.Length);
        midia.Tipo.Should().Be(TipoMidia.Imagem);
        midia.DuracaoSegundos.Should().Be(12);
        midia.PendenteChave.Should().BeNull();
        (await _context.ItensPlaylist.CountAsync(i => i.PlaylistId == playlist.Id && i.MidiaId == id)).Should().Be(1, "continua na grade");
        _armazenamento.Removidos.Should().Equal(chaveAntiga);
        _emails.Enfileirados.Last().Subject.Should().StartWith("Troca de arquivo aprovada");
    }

    [Fact]
    public async Task SubstituirArquivo_RecusaMantemOAtualApagaOPendenteEGuardaOMotivo()
    {
        var id = await CriarMidia(NovaMidia(Arquivos.Mp4(), TiposArquivoMidia.Mp4));
        await CriarPlaylistNoBancoAsync("Grade", id);
        var chaveAntiga = (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento;
        var primeira = Enviar(Arquivos.Png(), TiposArquivoMidia.Png);
        await Substituir(new SubstituirArquivoMidiaCommand { Id = id, Chave = primeira, TipoConteudo = TiposArquivoMidia.Png, Version = 0 });

        // Uma segunda troca antes da aprovação substitui a primeira (que sai do storage).
        var segunda = Enviar(Arquivos.Png(1500), TiposArquivoMidia.Png);
        await Substituir(new SubstituirArquivoMidiaCommand { Id = id, Chave = segunda, TipoConteudo = TiposArquivoMidia.Png, Version = 0 });
        _armazenamento.Removidos.Should().Equal(primeira);

        await new RecusarTrocaArquivoCommandHandler(_context, _armazenamento, _emails, new ConfiguracaoIndoorFake(),
                NullLogger<RecusarTrocaArquivoCommandHandler>.Instance)
            .Handle(new RecusarTrocaArquivoCommand { Id = id, Motivo = "Imagem cortada.", Version = 0 }, CancellationToken.None);

        var midia = await _context.Midias.AsNoTracking().SingleAsync(m => m.Id == id);
        midia.ChaveArmazenamento.Should().Be(chaveAntiga);
        midia.PendenteChave.Should().BeNull();
        midia.MotivoRecusaTroca.Should().Be("Imagem cortada.");
        _armazenamento.Removidos.Should().Equal(primeira, segunda);
        _emails.Enfileirados.Last().HtmlBody.Should().Contain("Imagem cortada.");

        var semPendente = () => new AprovarTrocaArquivoCommandHandler(_context, _armazenamento, _emails, new ConfiguracaoIndoorFake(),
                NullLogger<AprovarTrocaArquivoCommandHandler>.Instance)
            .Handle(new AprovarTrocaArquivoCommand(id, 0), CancellationToken.None);
        (await semPendente.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("PendenteChave");
    }

    [Fact]
    public async Task SubstituirArquivo_PendenteContaNaCota()
    {
        // Cota do cliente A: 1 MB. Em grade, o atual (700 KB) segue no ar: outro de 700 KB não cabe.
        var id = await CriarMidia(NovaMidia(Arquivos.Mp4(700 * 1024), TiposArquivoMidia.Mp4));
        await CriarPlaylistNoBancoAsync("Grade", id);
        var chave = Enviar(Arquivos.Mp4(700 * 1024), TiposArquivoMidia.Mp4);

        var acao = () => Substituir(new SubstituirArquivoMidiaCommand { Id = id, Chave = chave, TipoConteudo = TiposArquivoMidia.Mp4, Version = 0 });

        await acao.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubstituirArquivo_ConteudoInvalidoERecusadoEApagaSoONovo()
    {
        var id = await CriarMidia(NovaMidia(Arquivos.Mp4(), TiposArquivoMidia.Mp4));
        var chaveAntiga = (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento;
        var chaveNova = Enviar(Arquivos.Png(), TiposArquivoMidia.Mp4);

        var acao = () => Substituir(new SubstituirArquivoMidiaCommand { Id = id, Chave = chaveNova, TipoConteudo = TiposArquivoMidia.Mp4, Version = 0 });

        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("TipoConteudo");
        (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento.Should().Be(chaveAntiga);
        _armazenamento.Removidos.Should().Equal(chaveNova);
    }

    [Fact]
    public async Task SubstituirArquivo_CotaDescontaOArquivoQueSai()
    {
        // Cota do cliente A: 1 MB. Arquivo de 700 KB trocado por outro de 700 KB cabe.
        var grande = Arquivos.Mp4(700 * 1024);
        var id = await CriarMidia(NovaMidia(grande, TiposArquivoMidia.Mp4));
        var outro = Arquivos.Mp4(700 * 1024);
        var chave = Enviar(outro, TiposArquivoMidia.Mp4);

        await Substituir(new SubstituirArquivoMidiaCommand { Id = id, Chave = chave, TipoConteudo = TiposArquivoMidia.Mp4, Version = 0 });

        (await _context.Midias.SingleAsync(m => m.Id == id)).ChaveArmazenamento.Should().Be(chave);
    }

    [Fact]
    public async Task SubstituirArquivo_MidiaDeOutroClienteDeveDar404()
    {
        var midiaB = await CriarMidiaNoBancoAsync(_clienteB);
        var chave = Enviar(Arquivos.Png(), TiposArquivoMidia.Png);

        var acao = () => Substituir(new SubstituirArquivoMidiaCommand { Id = midiaB.Id, Chave = chave, TipoConteudo = TiposArquivoMidia.Png, Version = 0 });

        await acao.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Onde meus anúncios passam ----------

    [Fact]
    public async Task OndePassam_SoTelasAtivasComPlaylistAtivaQueTemMidiaAtivaDoCliente()
    {
        var minha = await CriarMidiaNoBancoAsync(_clienteA);
        var minhaInativa = await CriarMidiaNoBancoAsync(_clienteA, ativa: false);
        var deOutro = await CriarMidiaNoBancoAsync(_clienteB);

        var comMinha = await CriarPlaylistNoBancoAsync("Com a minha", deOutro.Id, minha.Id);
        var soDeOutro = await CriarPlaylistNoBancoAsync("Só de outro", deOutro.Id);
        var comInativa = await CriarPlaylistNoBancoAsync("Com a inativa", minhaInativa.Id);
        var inativa = await CriarPlaylistNoBancoAsync("Playlist inativa", minha.Id);
        inativa.Ativo = false;

        var local = new Parceiro { Nome = "Tapiocaria" };
        var outroLocal = new Parceiro { Nome = "Loja" };
        _context.Parceiros.AddRange(local, outroLocal);
        _context.Telas.AddRange(
            new Tela { Parceiro = local, Nome = "Padaria", Playlist = comMinha },
            new Tela { Parceiro = local, Nome = "Farmácia", Playlist = soDeOutro },
            new Tela { Parceiro = local, Nome = "Banca", Playlist = comInativa },
            new Tela { Parceiro = local, Nome = "Bar", Playlist = inativa },
            new Tela { Parceiro = local, Nome = "Desligada", Playlist = comMinha, Ativo = false },
            new Tela { Parceiro = outroLocal, Nome = "Minha loja" });
        await _context.SaveChangesAsync();

        var lista = await new GetTelasQueryHandler(_context, _clienteAtual, new IdentityNomesFake(), _relogio)
            .Handle(new GetTelasQuery(), CancellationToken.None);

        lista.Items.Select(t => t.Nome).Should().Equal("Padaria");
        lista.Items.Single().ParceiroNome.Should().Be("Tapiocaria", "o local é o parceiro");
        lista.Items.Single().CodigoPareamento.Should().BeNull();

        var foraDaLista = _context.Telas.Single(t => t.Nome == "Farmácia").Id;
        var obter = () => Telas().Handle(new GetTelaByIdQuery(foraDaLista), CancellationToken.None);
        await obter.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task OndePassam_SituacaoSegueASincronizacao()
    {
        var minha = await CriarMidiaNoBancoAsync(_clienteA);
        var playlist = await CriarPlaylistNoBancoAsync("Grade", minha.Id);
        var tela = new Tela { Parceiro = new Parceiro { Nome = "Padaria" }, Nome = "Padaria", Playlist = playlist };
        tela.Parear("tv-1", Agora.AddHours(-2));
        tela.Estado = new EstadoTela();
        tela.Estado.RegistrarSincronizacao(Agora.AddMinutes(-2), "1.0.0", "v", 3);
        _context.Telas.Add(tela);
        await _context.SaveChangesAsync();

        (await Telas().Handle(new GetTelaByIdQuery(tela.Id), CancellationToken.None)).Situacao.Should().Be(SituacaoTela.Online);

        _relogio.Agora = Agora.AddMinutes(10);
        (await Telas().Handle(new GetTelaByIdQuery(tela.Id), CancellationToken.None)).Situacao.Should().Be(SituacaoTela.Offline);
    }

    // ---------- Apoio ----------

    private GetTelaByIdQueryHandler Telas() => new(_context, _clienteAtual, new IdentityNomesFake(), _relogio);

    private Task<long> CriarMidia(CriarMidiaCommand comando) =>
        new CriarMidiaCommandHandler(_context, _clienteAtual, _armazenamento, NullLogger<CriarMidiaCommandHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    private Task<SubstituicaoArquivoDto> Substituir(SubstituirArquivoMidiaCommand comando) =>
        new SubstituirArquivoMidiaCommandHandler(_context, _clienteAtual, _armazenamento, _emails, new ConfiguracaoIndoorFake(), _relogio,
                NullLogger<SubstituirArquivoMidiaCommandHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    private CriarMidiaCommand NovaMidia(byte[] arquivo, string tipo) => new()
    {
        Titulo = "Mídia",
        Chave = Enviar(arquivo, tipo),
        TipoConteudo = tipo,
        DuracaoSegundos = 15,
    };

    private string Enviar(byte[] arquivo, string tipo)
    {
        var chave = ChavesArmazenamento.Gerar(_clienteA, tipo);
        _armazenamento.Arquivos[chave] = arquivo;
        return chave;
    }

    private async Task<Midia> CriarMidiaNoBancoAsync(long clienteId, long tamanho = 1000, int duracao = 15, bool ativa = true)
    {
        var midia = new Midia
        {
            Ativo = ativa,
            ClienteId = clienteId,
            Titulo = "Mídia",
            Tipo = TipoMidia.Video,
            ChaveArmazenamento = ChavesArmazenamento.Gerar(clienteId, TiposArquivoMidia.Mp4),
            TipoConteudo = TiposArquivoMidia.Mp4,
            TamanhoBytes = tamanho,
            Hash = new string('a', 64),
            DuracaoSegundos = duracao,
        };
        _context.Midias.Add(midia);
        await _context.SaveChangesAsync();
        return midia;
    }

    private async Task<Playlist> CriarPlaylistNoBancoAsync(string nome, params long[] midias)
    {
        var playlist = new Playlist { Nome = nome };
        playlist.DefinirItens(midias.Select(m => new ItemPlaylistInformado(null, m, DiasSemana.Todos)).ToList());
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();
        return playlist;
    }

    private static string Sha256(byte[] dados) => Convert.ToHexStringLower(SHA256.HashData(dados));
}
