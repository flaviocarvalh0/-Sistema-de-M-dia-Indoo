using System.IdentityModel.Tokens.Jwt;
using Atlas.Application.Business.Dispositivo.Autenticacao;
using Atlas.Application.Business.Dispositivo.Common;
using Atlas.Application.Business.Dispositivo.Sincronizacao;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Business.Dispositivo;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Pareamento, credencial e sincronização da TV.</summary>
public class DispositivoTests : IDisposable
{
    // Segunda-feira, 12h em Manaus (16h UTC).
    private static readonly DateTimeOffset Agora = new(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 10, 12);

    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly ArmazenamentoFake _armazenamento = new();
    private readonly RelogioFixo _relogio = new(Agora);
    private readonly GeradorTokenFake _gerador = new();
    private readonly Parceiro _parceiro;
    private readonly Tela _tela;

    public DispositivoTests()
    {
        _parceiro = new Parceiro { Nome = "Academia Centro", Email = "a@teste.local", FusoHorario = "America/Manaus" };
        _context.Parceiros.Add(_parceiro);
        _tela = new Tela { Parceiro = _parceiro, Nome = "Recepção" };
        _tela.GerarCodigoPareamento("AB12CD34", Agora.AddHours(-1));
        _context.Telas.Add(_tela);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ---------- Pareamento ----------

    [Fact]
    public async Task Autenticar_DeveParearConsumirOCodigoEDevolverToken()
    {
        var resultado = await Autenticar(" ab12-cd34 ", "tv-uuid-1", "1.0.0");

        resultado.TelaId.Should().Be(_tela.Id);
        resultado.NomeTela.Should().Be("Recepção");
        resultado.NomeParceiro.Should().Be("Academia Centro");
        resultado.IntervaloSincronizacaoSegundos.Should().Be(300);
        resultado.Token.Should().Be(_gerador.Ultimo);

        var tela = await _context.Telas.Include(t => t.Estado).SingleAsync();
        tela.DispositivoId.Should().Be("tv-uuid-1");
        tela.CodigoPareamento.Should().BeNull();
        tela.Estado!.VersaoApp.Should().Be("1.0.0");
        tela.Estado.UltimaSincronizacao.Should().Be(Agora);
        _gerador.Emitidos.Single().Should().Be((tela.Id, _parceiro.Id, "tv-uuid-1", tela.VersaoCredencial));
    }

    [Fact]
    public async Task Autenticar_CodigoUsadoDuasVezesDeveSerRecusado()
    {
        await Autenticar("AB12CD34", "tv-1");

        var acao = () => Autenticar("AB12CD34", "tv-2");

        await acao.Should().ThrowAsync<ValidationException>();
        (await _context.Telas.SingleAsync()).DispositivoId.Should().Be("tv-1");
    }

    [Fact]
    public async Task Autenticar_CodigoExpiradoDeveSerRecusado()
    {
        _relogio.Agora = Agora.AddHours(24);

        var acao = () => Autenticar("AB12CD34", "tv-1");

        (await acao.Should().ThrowAsync<ValidationException>())
            .Which.Errors["CodigoPareamento"].Should().Equal(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);
    }

    [Fact]
    public async Task Autenticar_ParceiroBloqueadoOuTelaInativaDaAMesmaRespostaQueCodigoErrado()
    {
        _parceiro.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();
        var bloqueado = () => Autenticar("AB12CD34", "tv-1");
        (await bloqueado.Should().ThrowAsync<ValidationException>())
            .Which.Errors["CodigoPareamento"].Should().Equal(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);

        _parceiro.Desbloquear();
        _tela.Ativo = false;
        await _context.SaveChangesAsync();
        var inativa = () => Autenticar("AB12CD34", "tv-1");
        (await inativa.Should().ThrowAsync<ValidationException>())
            .Which.Errors["CodigoPareamento"].Should().Equal(AutenticarDispositivoCommandHandler.MensagemCodigoInvalido);
    }

    [Fact]
    public async Task Autenticar_NovaTvNoLugarDeveRevogarAAnterior()
    {
        await Autenticar("AB12CD34", "tv-velha");
        var versaoVelha = _tela.VersaoCredencial;
        _tela.GerarCodigoPareamento("ZZ99YY88", Agora);
        await _context.SaveChangesAsync();

        await Autenticar("ZZ99YY88", "tv-nova");

        var antiga = () => Atual(new CredencialFake(_tela.Id, _parceiro.Id, "tv-velha", versaoVelha)).ObterAsync(CancellationToken.None);
        await antiga.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("ab12cd3", false)]
    [InlineData("AB12-CD34", true)]
    [InlineData("ab 12 cd 34", true)]
    [InlineData("", false)]
    public void Validador_ConfereOTamanhoDoCodigoNormalizado(string codigo, bool valido)
    {
        new AutenticarDispositivoCommandValidator()
            .Validate(new AutenticarDispositivoCommand { CodigoPareamento = codigo, DispositivoId = "tv" })
            .IsValid.Should().Be(valido);
    }

    // ---------- Credencial ----------

    [Fact]
    public async Task DispositivoAtual_DeveSeparar401De403()
    {
        await Autenticar("AB12CD34", "tv-1");
        var valida = new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial);

        (await Atual(valida).ObterAsync(CancellationToken.None)).TelaId.Should().Be(_tela.Id);

        // Token sem as claims, de outra TV, de credencial antiga ou de outro parceiro: 401 (parear de novo).
        await Esperar401(new CredencialFake(null, null, null, null));
        await Esperar401(valida with { DispositivoId = "tv-2" });
        await Esperar401(valida with { VersaoCredencial = _tela.VersaoCredencial - 1 });
        await Esperar401(valida with { ParceiroId = _parceiro.Id + 1 });

        // Tela inativa ou parceiro bloqueado: 403 (continua no que baixou).
        _tela.Ativo = false;
        await _context.SaveChangesAsync();
        var inativa = () => Atual(valida).ObterAsync(CancellationToken.None);
        await inativa.Should().ThrowAsync<ForbiddenAccessException>();

        _tela.Ativo = true;
        _parceiro.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();
        var bloqueado = () => Atual(valida).ObterAsync(CancellationToken.None);
        await bloqueado.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Desvincular_DeveRevogarOToken()
    {
        await Autenticar("AB12CD34", "tv-1");
        var credencial = new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial);

        _tela.Desvincular();
        await _context.SaveChangesAsync();

        await Esperar401(credencial);
    }

    // ---------- Sincronização ----------

    [Fact]
    public async Task Sincronizar_DeveEntregarAGradeComFiltrosEUrlsNumaChamadaSo()
    {
        var credencial = await ParearComGradeAsync(
            (Midia("Segunda a sexta"), DiasSemana.DiasUteis),
            (Midia("Só fim de semana"), DiasSemana.FimDeSemana),
            (Midia("Começa amanhã", inicio: Hoje.AddDays(1)), DiasSemana.Todos),
            (Midia("Venceu ontem", fim: Hoje.AddDays(-1)), DiasSemana.Todos),
            (Midia("Inativa", ativa: false), DiasSemana.Todos),
            (Midia("Termina hoje", fim: Hoje), DiasSemana.Todos));

        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoApp = "1.0.1", ItensProntos = 2 });

        grade.Alterada.Should().BeTrue();
        grade.Hoje.Should().Be(Hoje);
        grade.FusoHorario.Should().Be("America/Manaus");
        grade.NomeTela.Should().Be("Recepção");
        grade.Itens!.Select(i => (Titulo(i.MidiaId), i.ExibirHoje)).Should().Equal(
            ("Segunda a sexta", true),
            ("Só fim de semana", false),
            ("Começa amanhã", false),
            ("Termina hoje", true));
        grade.Itens!.Select(i => i.Ordem).Should().BeInAscendingOrder();
        grade.Itens![0].DiasSemana.Should().Be((int)DiasSemana.DiasUteis);
        grade.Itens![0].Url.ToString().Should().Contain("s=86400");
        grade.Itens![0].Hash.Should().HaveLength(64);
        _armazenamento.ChamadasEmLote.Should().Be(1);

        var estado = await _context.EstadosTela.SingleAsync();
        estado.UltimaSincronizacao.Should().Be(Agora);
        estado.VersaoApp.Should().Be("1.0.1");
        estado.ItensProntos.Should().Be(2);
        estado.VersaoGrade.Should().Be(grade.VersaoGrade);
    }

    [Fact]
    public async Task Sincronizar_ComAMesmaVersaoNaoDeveMandarItensNemGerarUrls()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var primeira = await Sincronizar(credencial, new GetSincronizacaoQuery());

        var segunda = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = primeira.VersaoGrade });

        segunda.Alterada.Should().BeFalse();
        segunda.Itens.Should().BeNull();
        segunda.VersaoGrade.Should().Be(primeira.VersaoGrade);
        _armazenamento.ChamadasEmLote.Should().Be(1, "a segunda sincronização não precisa de URLs");
    }

    [Fact]
    public async Task Sincronizar_MudarAGradeDeveMudarAVersao()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var antes = await Sincronizar(credencial, new GetSincronizacaoQuery());

        var item = await _context.ItensPlaylist.SingleAsync();
        item.DiasSemana = DiasSemana.Segunda;
        await _context.SaveChangesAsync();

        var depois = await Sincronizar(credencial, new GetSincronizacaoQuery { VersaoGrade = antes.VersaoGrade });

        depois.Alterada.Should().BeTrue();
        depois.VersaoGrade.Should().NotBe(antes.VersaoGrade);
        depois.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task Sincronizar_PlaylistInativaOuAusenteDaGradeVazia()
    {
        var credencial = await ParearComGradeAsync((Midia("Vídeo"), DiasSemana.Todos));
        var playlist = await _context.Playlists.SingleAsync();
        playlist.Ativo = false;
        await _context.SaveChangesAsync();

        var inativa = await Sincronizar(credencial, new GetSincronizacaoQuery());
        inativa.Itens.Should().BeEmpty();

        _tela.PlaylistId = null;
        await _context.SaveChangesAsync();
        var semPlaylist = await Sincronizar(credencial, new GetSincronizacaoQuery());
        semPlaylist.Itens.Should().BeEmpty();
        semPlaylist.VersaoGrade.Should().NotBe(inativa.VersaoGrade, "sem playlist é outra grade, mesmo vazia");
    }

    [Fact]
    public async Task Sincronizar_DeveRenovarOTokenPertoDeVencer()
    {
        var credencial = await ParearComGradeAsync();

        (await Sincronizar(credencial with { ExpiraEm = Agora.AddDays(20) }, new GetSincronizacaoQuery()))
            .NovoToken.Should().BeNull();

        var renovada = await Sincronizar(credencial with { ExpiraEm = Agora.AddDays(5) }, new GetSincronizacaoQuery());
        renovada.NovoToken.Should().NotBeNull();
        renovada.NovoToken!.Token.Should().Be(_gerador.Ultimo);
    }

    [Fact]
    public async Task Sincronizar_HojeEOdoFusoDoParceiro()
    {
        var credencial = await ParearComGradeAsync((Midia("Segunda"), DiasSemana.Segunda));

        // 02h UTC de terça ainda é segunda (22h) em Manaus.
        _relogio.Agora = new DateTimeOffset(2026, 10, 13, 2, 0, 0, TimeSpan.Zero);
        var grade = await Sincronizar(credencial, new GetSincronizacaoQuery());

        grade.Hoje.Should().Be(Hoje);
        grade.Itens!.Single().ExibirHoje.Should().BeTrue();
    }

    [Fact]
    public void Versao_DependeDoConteudoEDaOrdem()
    {
        static GetSincronizacaoQueryHandler.ItemGrade Item(int ordem, long midia) =>
            new(ordem, midia, TipoMidia.Video, "video/mp4", "c", new string('a', 64), 1, 15, DiasSemana.Todos, null, null);

        var ab = GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 1), Item(1, 2)]);

        ab.Should().HaveLength(64)
            .And.Be(GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 1), Item(1, 2)]));
        ab.Should().NotBe(GetSincronizacaoQueryHandler.CalcularVersao(1, [Item(0, 2), Item(1, 1)]));
        ab.Should().NotBe(GetSincronizacaoQueryHandler.CalcularVersao(2, [Item(0, 1), Item(1, 2)]));
    }

    // ---------- Token de verdade ----------

    [Fact]
    public void Token_ValeSoNoEsquemaDaTv()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Atlas",
                ["Jwt:Key"] = new string('k', 64),
            })
            .Build();
        var token = new GeradorTokenDispositivo(configuracao, new ConfiguracaoIndoorFake(), TimeProvider.System).Gerar(7, 3, "tv-1", 2);
        var (emissor, chave) = AutenticacaoDispositivo.LerChave(configuracao);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var principal = handler.ValidateToken(token.Token, new TokenValidationParameters
        {
            ValidIssuer = emissor,
            ValidAudience = AutenticacaoDispositivo.Audiencia,
            IssuerSigningKey = chave,
        }, out _);

        principal.FindFirst(ClaimsDispositivo.TelaId)!.Value.Should().Be("7");
        principal.FindFirst(ClaimsDispositivo.ParceiroId)!.Value.Should().Be("3");
        principal.FindFirst(ClaimsDispositivo.DispositivoId)!.Value.Should().Be("tv-1");
        principal.FindFirst(ClaimsDispositivo.VersaoCredencial)!.Value.Should().Be("2");
        token.ExpiraEm.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));

        // Na audiência dos usuários da plataforma o token não vale.
        var comoUsuario = () => handler.ValidateToken(token.Token, new TokenValidationParameters
        {
            ValidIssuer = emissor,
            ValidAudience = "Atlas.Clients",
            IssuerSigningKey = chave,
        }, out _);
        comoUsuario.Should().Throw<SecurityTokenInvalidAudienceException>();
    }

    // ---------- Apoio ----------

    private async Task<DispositivoAutenticadoDto> Autenticar(string codigo, string dispositivo, string? versaoApp = null) =>
        await new AutenticarDispositivoCommandHandler(_context, _gerador, new ConfiguracaoIndoorFake(), _relogio)
            .Handle(new AutenticarDispositivoCommand { CodigoPareamento = codigo, DispositivoId = dispositivo, VersaoApp = versaoApp }, CancellationToken.None);

    private DispositivoAtual Atual(CredencialFake credencial) => new(credencial, _context);

    private async Task Esperar401(CredencialFake credencial)
    {
        var acao = () => Atual(credencial).ObterAsync(CancellationToken.None);
        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private Task<SincronizacaoDto> Sincronizar(CredencialFake credencial, GetSincronizacaoQuery query) =>
        new GetSincronizacaoQueryHandler(
                _context, Atual(credencial), _armazenamento, _gerador, new ConfiguracaoIndoorFake(), _relogio,
                NullLogger<GetSincronizacaoQueryHandler>.Instance)
            .Handle(query, CancellationToken.None);

    /// <summary>Pareia a TV e põe a playlist com os itens informados na tela.</summary>
    private async Task<CredencialFake> ParearComGradeAsync(params (Midia Midia, DiasSemana Dias)[] itens)
    {
        foreach (var (midia, _) in itens)
        {
            _context.Midias.Add(midia);
            _armazenamento.Arquivos[midia.ChaveArmazenamento] = [1];
        }

        await _context.SaveChangesAsync();

        var playlist = new Playlist { ParceiroId = _parceiro.Id, Nome = "Grade" };
        playlist.DefinirItens(itens.Select(i => new ItemPlaylistInformado(null, i.Midia.Id, i.Dias)).ToList());
        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();

        _tela.PlaylistId = playlist.Id;
        await _context.SaveChangesAsync();

        await Autenticar("AB12CD34", "tv-1");
        return new CredencialFake(_tela.Id, _parceiro.Id, "tv-1", _tela.VersaoCredencial, Agora.AddDays(30));
    }

    private Midia Midia(string titulo, DateOnly? inicio = null, DateOnly? fim = null, bool ativa = true) => new()
    {
        ParceiroId = _parceiro.Id,
        Titulo = titulo,
        Tipo = TipoMidia.Video,
        ChaveArmazenamento = ChavesArmazenamento.Gerar(_parceiro.Id, TiposArquivoMidia.Mp4),
        TipoConteudo = TiposArquivoMidia.Mp4,
        TamanhoBytes = 1000,
        Hash = new string('c', 64),
        InicioExibicao = inicio,
        FimExibicao = fim,
        Ativo = ativa,
    };

    private string Titulo(long midiaId) => _context.Midias.Single(m => m.Id == midiaId).Titulo;

    private sealed record CredencialFake(
        long? TelaId,
        long? ParceiroId,
        string? DispositivoId,
        int? VersaoCredencial,
        DateTimeOffset? ExpiraEm = null) : ICredencialDispositivo;

    private sealed class GeradorTokenFake : IGeradorTokenDispositivo
    {
        public List<(long TelaId, long ParceiroId, string DispositivoId, int Versao)> Emitidos { get; } = [];
        public string? Ultimo { get; private set; }

        public TokenDispositivo Gerar(long telaId, long parceiroId, string dispositivoId, int versaoCredencial)
        {
            Emitidos.Add((telaId, parceiroId, dispositivoId, versaoCredencial));
            Ultimo = $"token-{Emitidos.Count}";
            return new TokenDispositivo(Ultimo, DateTimeOffset.UnixEpoch);
        }
    }
}
