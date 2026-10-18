using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Infrastructure.Business.Armazenamento;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

public class TiposArquivoMidiaTests
{
    public static TheoryData<byte[], string?> Assinaturas => new()
    {
        { [0x00, 0x00, 0x00, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m'], TiposArquivoMidia.Mp4 },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10], TiposArquivoMidia.Jpeg },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], TiposArquivoMidia.Png },
        { [0x1A, 0x45, 0xDF, 0xA3, 0x00, 0x00, 0x00, 0x00], null },              // WebM/MKV
        { "GIF89a"u8.ToArray(), null },
        { [0x00, 0x00], null },
    };

    [Theory]
    [MemberData(nameof(Assinaturas))]
    public void Detectar_DevePeloInicioDoArquivo(byte[] inicio, string? esperado)
    {
        TiposArquivoMidia.Detectar(inicio).Should().Be(esperado);
    }

    [Fact]
    public void Tipos_DevemMapearTipoEExtensao()
    {
        TiposArquivoMidia.TipoDaMidia(TiposArquivoMidia.Mp4).Should().Be(TipoMidia.Video);
        TiposArquivoMidia.TipoDaMidia(TiposArquivoMidia.Png).Should().Be(TipoMidia.Imagem);
        TiposArquivoMidia.Extensao(TiposArquivoMidia.Jpeg).Should().Be(".jpg");
        TiposArquivoMidia.Aceito("video/webm").Should().BeFalse();
        TiposArquivoMidia.Aceito(null).Should().BeFalse();
    }
}

public class ChavesArmazenamentoTests
{
    [Fact]
    public void Gerar_DeveFicarNaPastaDoClienteComAExtensaoDoTipo()
    {
        var chave = ChavesArmazenamento.Gerar(42, TiposArquivoMidia.Mp4);

        chave.Should().MatchRegex("^clientes/42/midias/[0-9a-f]{32}\\.mp4$");
        ChavesArmazenamento.Valida(chave, 42, TiposArquivoMidia.Mp4).Should().BeTrue();
    }

    [Theory]
    [InlineData("clientes/7/midias/0123456789abcdef0123456789abcdef.mp4", "video/mp4", true)]
    [InlineData("clientes/8/midias/0123456789abcdef0123456789abcdef.mp4", "video/mp4", false)]   // outro cliente
    [InlineData("clientes/7/midias/0123456789abcdef0123456789abcdef.png", "video/mp4", false)]   // extensão de outro tipo
    [InlineData("clientes/7/midias/../8/midias/0123456789abcdef0123456789.mp4", "video/mp4", false)]
    [InlineData("clientes/7/midias/sub/0123456789abcdef0123456789abcd.mp4", "video/mp4", false)]
    [InlineData("clientes/7/midias/0123456789ABCDEF0123456789ABCDEF.mp4", "video/mp4", false)]   // maiúsculas
    [InlineData("clientes/70/midias/0123456789abcdef0123456789abcdef.mp4", "video/mp4", false)]
    [InlineData("clientes/7/midias/0123456789abcdef0123456789abcdef.mp4", "video/webm", false)]
    [InlineData(null, "video/mp4", false)]
    public void Valida_SoAceitaAChaveGeradaParaOCliente(string? chave, string tipo, bool esperado)
    {
        ChavesArmazenamento.Valida(chave, 7, tipo).Should().Be(esperado);
    }
}

public class VerificadorArquivoMidiaTests
{
    [Fact]
    public async Task Verificar_DeveCalcularSha256TamanhoETipo()
    {
        var conteudo = new byte[200_000];
        new Random(7).NextBytes(conteudo);
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(conteudo, 0);

        var resultado = await VerificadorArquivoMidia.VerificarAsync(new LeituraPicada(conteudo), CancellationToken.None);

        resultado.Hash.Should().Be(Convert.ToHexStringLower(SHA256.HashData(conteudo)));
        resultado.TamanhoBytes.Should().Be(conteudo.Length);
        resultado.TipoDetectado.Should().Be(TiposArquivoMidia.Png);
        resultado.ExcedeLimite.Should().BeFalse();
    }

    [Fact]
    public async Task Verificar_DeveParardeLerAcimaDoLimite()
    {
        var resultado = await VerificadorArquivoMidia.VerificarAsync(new ZerosInfinitos(), CancellationToken.None);

        resultado.ExcedeLimite.Should().BeTrue();
        resultado.Hash.Should().BeNull();
        resultado.TamanhoBytes.Should().BeGreaterThan(Midia.TamanhoMaximoBytes).And.BeLessThan(Midia.TamanhoMaximoBytes + 100_000);
    }

    [Fact]
    public async Task Verificar_ArquivoVazioNaoTemTipo()
    {
        var resultado = await VerificadorArquivoMidia.VerificarAsync(new MemoryStream(), CancellationToken.None);

        resultado.TamanhoBytes.Should().Be(0);
        resultado.TipoDetectado.Should().BeNull();
    }

    /// <summary>Entrega no máximo 5 bytes por leitura: a assinatura chega picada, como na rede.</summary>
    private sealed class LeituraPicada(byte[] dados) : MemoryStream(dados)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(5, buffer.Length)], cancellationToken);
    }

    private sealed class ZerosInfinitos : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public class ArmazenamentoSupabaseTests
{
    private const string Chave = "clientes/7/midias/0123456789abcdef0123456789abcdef.mp4";
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly SupabaseFake _supabase = new();

    [Fact]
    public async Task PrepararEnvio_DeveAssinarAChaveEDevolverAUrlAbsoluta()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"url":"/object/upload/sign/midias/clientes/7/midias/x.mp4?token=abc","token":"abc"}""");

        var envio = await Armazenamento().PrepararEnvioAsync(Chave, CancellationToken.None);

        var requisicao = _supabase.Requisicoes.Single();
        requisicao.Metodo.Should().Be(HttpMethod.Post);
        requisicao.Url.Should().Be($"https://projeto.supabase.test/storage/v1/object/upload/sign/midias/{Chave}");
        requisicao.Authorization.Should().Be("Bearer chave-servico");
        requisicao.ApiKey.Should().Be("chave-servico");

        envio.Url.Should().Be(new Uri("https://projeto.supabase.test/storage/v1/object/upload/sign/midias/clientes/7/midias/x.mp4?token=abc"));
        envio.ExpiraEm.Should().Be(Agora.AddHours(2));
    }

    [Fact]
    public async Task GerarUrlLeitura_DeveMandarAValidadeEmSegundos()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"signedURL":"/object/sign/midias/clientes/7/midias/x.mp4?token=xyz"}""");

        var url = await Armazenamento().GerarUrlLeituraAsync(Chave, TimeSpan.FromHours(6), CancellationToken.None);

        var requisicao = _supabase.Requisicoes.Single();
        requisicao.Url.Should().Be($"https://projeto.supabase.test/storage/v1/object/sign/midias/{Chave}");
        JsonDocument.Parse(requisicao.Corpo!).RootElement.GetProperty("expiresIn").GetInt32().Should().Be(21600);
        url.Should().Be(new Uri("https://projeto.supabase.test/storage/v1/object/sign/midias/clientes/7/midias/x.mp4?token=xyz"));
    }

    [Fact]
    public async Task Obter_DeveLerTamanhoETipoDosCabecalhos()
    {
        _supabase.Responder(HttpStatusCode.OK, conteudo: new byte[1234], tipoConteudo: "video/mp4");

        var objeto = await Armazenamento().ObterAsync(Chave, CancellationToken.None);

        _supabase.Requisicoes.Single().Should().BeEquivalentTo(new
        {
            Metodo = HttpMethod.Head,
            Url = $"https://projeto.supabase.test/storage/v1/object/authenticated/midias/{Chave}",
        }, o => o.ExcludingMissingMembers());
        objeto.Should().Be(new ObjetoArmazenado(1234, "video/mp4"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)] // o Supabase responde 400 "Object not found"
    public async Task Obter_ObjetoInexistenteDeveDarNulo(HttpStatusCode status)
    {
        _supabase.Responder(status, """{"statusCode":"404","error":"not_found","message":"Object not found"}""");
        _supabase.Responder(status, """{"statusCode":"404","error":"not_found","message":"Object not found"}""");

        (await Armazenamento().ObterAsync(Chave, CancellationToken.None)).Should().BeNull();
        (await Armazenamento().AbrirLeituraAsync(Chave, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task AbrirLeitura_DeveDevolverOConteudo()
    {
        _supabase.Responder(HttpStatusCode.OK, conteudo: "conteúdo do vídeo"u8.ToArray(), tipoConteudo: "video/mp4");

        await using var leitura = await Armazenamento().AbrirLeituraAsync(Chave, CancellationToken.None);

        using var leitor = new StreamReader(leitura!, Encoding.UTF8);
        (await leitor.ReadToEndAsync()).Should().Be("conteúdo do vídeo");
        _supabase.Requisicoes.Single().Metodo.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task Remover_DeveApagarENaoFalharSeJaNaoExiste()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"message":"Successfully deleted"}""");
        _supabase.Responder(HttpStatusCode.BadRequest, """{"message":"Object not found"}""");

        await Armazenamento().RemoverAsync(Chave, CancellationToken.None);
        await Armazenamento().RemoverAsync(Chave, CancellationToken.None);

        _supabase.Requisicoes.Should().AllSatisfy(r =>
        {
            r.Metodo.Should().Be(HttpMethod.Delete);
            r.Url.Should().Be($"https://projeto.supabase.test/storage/v1/object/midias/{Chave}");
        });
    }

    [Fact]
    public async Task ErroDoStorage_DeveVirarIndisponivelSemExporAChave()
    {
        _supabase.Responder(HttpStatusCode.InternalServerError, """{"message":"internal"}""");

        var acao = () => Armazenamento().PrepararEnvioAsync(Chave, CancellationToken.None);

        (await acao.Should().ThrowAsync<ArmazenamentoIndisponivelException>())
            .Which.Message.Should().Contain("500").And.NotContain("chave-servico");
    }

    [Fact]
    public async Task FalhaDeRede_DeveVirarIndisponivel()
    {
        _supabase.FalharRede = true;

        var acao = () => Armazenamento().ObterAsync(Chave, CancellationToken.None);

        await acao.Should().ThrowAsync<ArmazenamentoIndisponivelException>();
    }

    [Fact]
    public async Task GarantirBucket_ExistenteNaoDeveCriar()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"id":"midias","name":"midias","public":false}""");

        await Armazenamento().GarantirBucketAsync(CancellationToken.None);

        _supabase.Requisicoes.Single().Url.Should().Be("https://projeto.supabase.test/storage/v1/bucket/midias");
    }

    [Fact]
    public async Task GarantirBucket_InexistenteDeveCriarPrivadoComOsLimites()
    {
        _supabase.Responder(HttpStatusCode.BadRequest, """{"message":"Bucket not found"}""");
        _supabase.Responder(HttpStatusCode.OK, """{"name":"midias"}""");

        await Armazenamento().GarantirBucketAsync(CancellationToken.None);

        var criacao = _supabase.Requisicoes[1];
        criacao.Metodo.Should().Be(HttpMethod.Post);
        criacao.Url.Should().Be("https://projeto.supabase.test/storage/v1/bucket");
        var corpo = JsonDocument.Parse(criacao.Corpo!).RootElement;
        corpo.GetProperty("id").GetString().Should().Be("midias");
        corpo.GetProperty("public").GetBoolean().Should().BeFalse();
        corpo.GetProperty("file_size_limit").GetInt64().Should().Be(Midia.TamanhoMaximoBytes);
        corpo.GetProperty("allowed_mime_types").EnumerateArray().Select(t => t.GetString())
            .Should().BeEquivalentTo("video/mp4", "image/jpeg", "image/png", "image/webp", "application/vnd.android.package-archive");
    }

    [Fact]
    public async Task GarantirBucket_ExistenteSemWebpGanhaOTipo()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"id":"midias","public":false,"allowed_mime_types":["video/mp4","image/jpeg","image/png"]}""");
        _supabase.Responder(HttpStatusCode.OK, """{"message":"Successfully updated"}""");

        await Armazenamento().GarantirBucketAsync(CancellationToken.None);

        var atualizacao = _supabase.Requisicoes[1];
        atualizacao.Metodo.Should().Be(HttpMethod.Put);
        atualizacao.Url.Should().Be("https://projeto.supabase.test/storage/v1/bucket/midias");
        var corpo = JsonDocument.Parse(atualizacao.Corpo!).RootElement;
        corpo.GetProperty("public").GetBoolean().Should().BeFalse();
        corpo.GetProperty("allowed_mime_types").EnumerateArray().Select(t => t.GetString())
            .Should().BeEquivalentTo("video/mp4", "image/jpeg", "image/png", "image/webp", "application/vnd.android.package-archive");
    }

    [Fact]
    public async Task GarantirBucket_ComTodosOsTiposNaoAtualiza()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"id":"midias","allowed_mime_types":["video/mp4","image/jpeg","image/png","image/webp","application/vnd.android.package-archive"]}""");

        await Armazenamento().GarantirBucketAsync(CancellationToken.None);

        _supabase.Requisicoes.Should().ContainSingle();
    }

    [Fact]
    public async Task Gravar_EnviaOConteudoComUpsert()
    {
        _supabase.Responder(HttpStatusCode.OK, """{"Key":"midias/noticias/1/abc.webp"}""");

        await Armazenamento().GravarAsync("noticias/1/abc.webp", [1, 2, 3], "image/webp", CancellationToken.None);

        var envio = _supabase.Requisicoes.Single();
        envio.Metodo.Should().Be(HttpMethod.Post);
        envio.Url.Should().Be("https://projeto.supabase.test/storage/v1/object/midias/noticias/1/abc.webp");
    }

    [Fact]
    public void Opcoes_SemUrlOuChaveDevemDarNulo()
    {
        OpcoesSupabase.Ler(Configuracao(("Url", "https://projeto.supabase.co"))).Should().BeNull();
        OpcoesSupabase.Ler(Configuracao(("ChaveServico", "x"))).Should().BeNull();
    }

    [Fact]
    public void Opcoes_DevemExigirHttpsForaDaMaquinaLocal()
    {
        var acao = () => OpcoesSupabase.Ler(Configuracao(("Url", "http://projeto.supabase.co"), ("ChaveServico", "x")));

        acao.Should().Throw<InvalidOperationException>();
        OpcoesSupabase.Ler(Configuracao(("Url", "http://localhost:54321"), ("ChaveServico", "x")))!
            .BaseStorage.Should().Be(new Uri("http://localhost:54321/storage/v1/"));
    }

    [Fact]
    public void Opcoes_DevemLerBucketECriacao()
    {
        var opcoes = OpcoesSupabase.Ler(Configuracao(
            ("Url", "https://projeto.supabase.co/"), ("ChaveServico", "x"), ("Bucket", "midias-homolog"), ("CriarBucket", "false")))!;

        opcoes.Bucket.Should().Be("midias-homolog");
        opcoes.CriarBucket.Should().BeFalse();
        opcoes.BaseStorage.Should().Be(new Uri("https://projeto.supabase.co/storage/v1/"));
    }

    [Fact]
    public async Task NaoConfigurado_DeveRecusarComMensagemClara()
    {
        var acao = () => new ArmazenamentoNaoConfigurado().PrepararEnvioAsync(Chave, CancellationToken.None);

        (await acao.Should().ThrowAsync<ArmazenamentoIndisponivelException>())
            .Which.Message.Should().Contain("Indoor:Armazenamento:Supabase");
    }

    private ArmazenamentoSupabase Armazenamento() => new(
        new HttpClient(_supabase),
        new OpcoesSupabase { Url = new Uri("https://projeto.supabase.test/"), ChaveServico = "chave-servico" },
        new RelogioFixo(),
        NullLogger<ArmazenamentoSupabase>.Instance);

    private static IConfiguration Configuracao(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => KeyValuePair.Create<string, string?>($"{OpcoesSupabase.Secao}:{v.Chave}", v.Valor)))
            .Build();

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed record RequisicaoRecebida(HttpMethod Metodo, string Url, string? Authorization, string? ApiKey, string? Corpo);

    /// <summary>Responde em ordem as respostas programadas e guarda as requisições.</summary>
    private sealed class SupabaseFake : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _respostas = new();

        public List<RequisicaoRecebida> Requisicoes { get; } = [];
        public bool FalharRede { get; set; }

        public void Responder(HttpStatusCode status, string? json = null, byte[]? conteudo = null, string? tipoConteudo = null) =>
            _respostas.Enqueue(() =>
            {
                var resposta = new HttpResponseMessage(status);
                if (json is not null)
                {
                    resposta.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else if (conteudo is not null)
                {
                    resposta.Content = new ByteArrayContent(conteudo);
                    resposta.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipoConteudo!);
                }

                return resposta;
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FalharRede)
            {
                throw new HttpRequestException("Conexão recusada.");
            }

            Requisicoes.Add(new RequisicaoRecebida(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("apikey", out var chave) ? chave.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return _respostas.Dequeue()();
        }
    }
}
