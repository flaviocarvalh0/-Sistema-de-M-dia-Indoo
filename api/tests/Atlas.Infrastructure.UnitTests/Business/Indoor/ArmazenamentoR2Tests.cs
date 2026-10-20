using System.Net;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Infrastructure.Business.Armazenamento;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>O cliente do Cloudflare R2 (ADR-025) contra um servidor S3 falso.</summary>
public class ArmazenamentoR2Tests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private readonly S3Fake _s3 = new();

    private static readonly OpcoesR2 Opcoes = new()
    {
        Conta = "conta",
        ChaveAcesso = "chave-acesso",
        SegredoAcesso = "segredo-acesso",
        Bucket = "midias",
        Endereco = new Uri("https://conta.r2.cloudflarestorage.com"),
    };

    private ArmazenamentoR2 R2() => new(new HttpClient(_s3), Opcoes, new RelogioFixo(Agora), NullLogger<ArmazenamentoR2>.Instance);

    [Fact]
    public async Task PrepararEnvio_DeveAssinarTipoETamanhoSemChamarOR2()
    {
        var envio = await R2().PrepararEnvioAsync("clientes/7/midias/a b.mp4", "video/mp4", 1234, CancellationToken.None);

        _s3.Requisicoes.Should().BeEmpty();
        envio.ExpiraEm.Should().Be(Agora + ArmazenamentoR2.ValidadeEnvio);
        var url = envio.Url.AbsoluteUri;
        url.Should().StartWith("https://conta.r2.cloudflarestorage.com/midias/clientes/7/midias/a%20b.mp4?");
        url.Should().Contain("X-Amz-SignedHeaders=content-length%3Bcontent-type%3Bhost");
        url.Should().Contain("X-Amz-Expires=7200");
        url.Should().Contain("X-Amz-Credential=chave-acesso%2F20261020%2Fauto%2Fs3%2Faws4_request");
        url.Should().NotContain("segredo-acesso");
    }

    [Fact]
    public async Task UrlDeLeitura_NaoDevePassarDe7Dias()
    {
        var url = await R2().GerarUrlLeituraAsync("a.mp4", TimeSpan.FromDays(30), CancellationToken.None);

        url.Query.Should().Contain("X-Amz-Expires=604800").And.Contain("X-Amz-SignedHeaders=host");
        _s3.Requisicoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Obter_DeveLerTamanhoETipoDoHead()
    {
        _s3.Responder(HttpStatusCode.OK, conteudo: new byte[321], tipo: "image/png");

        var objeto = await R2().ObterAsync("clientes/7/midias/a.png", CancellationToken.None);

        objeto.Should().Be(new ObjetoArmazenado(321, "image/png"));
        var requisicao = _s3.Requisicoes.Single();
        requisicao.Metodo.Should().Be(HttpMethod.Head);
        requisicao.Authorization.Should().StartWith("AWS4-HMAC-SHA256 Credential=chave-acesso/20261020/auto/s3/aws4_request")
            .And.Contain("SignedHeaders=host;x-amz-content-sha256;x-amz-date");
    }

    [Fact]
    public async Task Obter_404DeveSerNulo_ERemover404DeveSerOk()
    {
        _s3.Responder(HttpStatusCode.NotFound);
        _s3.Responder(HttpStatusCode.NotFound);

        (await R2().ObterAsync("x", CancellationToken.None)).Should().BeNull();
        await R2().RemoverAsync("x", CancellationToken.None);

        _s3.Requisicoes[1].Metodo.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task Gravar_DeveAssinarOHashDoConteudo()
    {
        _s3.Responder(HttpStatusCode.OK);
        var dados = Encoding.UTF8.GetBytes("imagem");

        await R2().GravarAsync("noticias/1/a.jpg", dados, "image/jpeg", CancellationToken.None);

        var requisicao = _s3.Requisicoes.Single();
        requisicao.Metodo.Should().Be(HttpMethod.Put);
        requisicao.HashPayload.Should().Be(Convert.ToHexStringLower(SHA256.HashData(dados)));
        requisicao.Authorization.Should().Contain("SignedHeaders=content-type;host;x-amz-content-sha256;x-amz-date");
    }

    [Fact]
    public async Task Listar_DevePaginarPeloTokenDeContinuacao()
    {
        _s3.Responder(HttpStatusCode.OK, xml: Pagina(truncada: true, ("a.mp4", 10), ("b.mp4", 20)));
        _s3.Responder(HttpStatusCode.OK, xml: Pagina(truncada: false, ("c.mp4", 30)));

        var objetos = new List<ObjetoListado>();
        await foreach (var o in R2().ListarAsync(CancellationToken.None))
        {
            objetos.Add(o);
        }

        objetos.Select(o => (o.Chave, o.TamanhoBytes)).Should().Equal(("a.mp4", 10L), ("b.mp4", 20L), ("c.mp4", 30L));
        objetos[0].ModificadoEm.Should().Be(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        _s3.Requisicoes[0].Url.Should().Be("https://conta.r2.cloudflarestorage.com/midias?list-type=2&max-keys=1000");
        _s3.Requisicoes[1].Url.Should().Contain("continuation-token=prox%2F%3D%3D");
    }

    [Fact]
    public async Task Cors_DeveLiberarAOrigemDoPainelComMd5()
    {
        _s3.Responder(HttpStatusCode.OK);

        await R2().ConfigurarCorsAsync(["https://painel.indoor.test/"], CancellationToken.None);

        var requisicao = _s3.Requisicoes.Single();
        requisicao.Url.Should().Be("https://conta.r2.cloudflarestorage.com/midias?cors=");
        requisicao.Corpo.Should().Contain("<AllowedOrigin>https://painel.indoor.test</AllowedOrigin>").And.Contain("<AllowedMethod>PUT</AllowedMethod>");
        requisicao.Md5.Should().Be(Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(requisicao.Corpo!))));
        requisicao.Authorization.Should().Contain("content-md5");
    }

    [Fact]
    public async Task Recusa_DeveVirarIndisponivelSemExporOSegredo()
    {
        _s3.Responder(HttpStatusCode.Forbidden, xml: "<Error><Code>AccessDenied</Code></Error>");

        var acao = () => R2().ObterAsync("x", CancellationToken.None);

        (await acao.Should().ThrowAsync<ArmazenamentoIndisponivelException>())
            .Which.Message.Should().Contain("403").And.NotContain("segredo");
    }

    [Fact]
    public async Task FalhaDeRede_DeveVirarIndisponivel()
    {
        _s3.FalharRede = true;

        var acao = () => R2().RemoverAsync("x", CancellationToken.None);

        await acao.Should().ThrowAsync<ArmazenamentoIndisponivelException>();
    }

    [Fact]
    public void Opcoes_SemSegredoDeveSerNulo_ComContaMontaOEndereco()
    {
        OpcoesR2.Ler(Configuracao(("Conta", "abc"), ("ChaveAcesso", "k"))).Should().BeNull();

        var opcoes = OpcoesR2.Ler(Configuracao(("Conta", " abc "), ("ChaveAcesso", "k"), ("SegredoAcesso", "s")))!;

        opcoes.Endereco.Should().Be(new Uri("https://abc.r2.cloudflarestorage.com"));
        opcoes.Bucket.Should().Be("midias");
        opcoes.Objeto("clientes/1/x.mp4").AbsoluteUri.Should().Be("https://abc.r2.cloudflarestorage.com/midias/clientes/1/x.mp4");
    }

    [Fact]
    public void Opcoes_EnderecoSemHttpsDeveSerRecusado()
    {
        var acao = () => OpcoesR2.Ler(Configuracao(("Conta", "abc"), ("ChaveAcesso", "k"), ("SegredoAcesso", "s"), ("Endereco", "http://r2.exemplo.com")));

        acao.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration Configuracao(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>($"{OpcoesR2.Secao}:{v.Chave}", v.Valor)))
            .Build();

    private static string Pagina(bool truncada, params (string Chave, long Tamanho)[] objetos) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
          <Name>midias</Name>
          {string.Concat(objetos.Select(o => $"<Contents><Key>{o.Chave}</Key><LastModified>2026-10-01T12:00:00.000Z</LastModified><Size>{o.Tamanho}</Size></Contents>"))}
          <IsTruncated>{(truncada ? "true" : "false")}</IsTruncated>
          {(truncada ? "<NextContinuationToken>prox/==</NextContinuationToken>" : string.Empty)}
        </ListBucketResult>
        """;

    private sealed record RequisicaoS3(HttpMethod Metodo, string Url, string? Authorization, string? HashPayload, string? Md5, string? Corpo);

    private sealed class S3Fake : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _respostas = new();

        public List<RequisicaoS3> Requisicoes { get; } = [];
        public bool FalharRede { get; set; }

        public void Responder(HttpStatusCode status, string? xml = null, byte[]? conteudo = null, string? tipo = null) =>
            _respostas.Enqueue(() =>
            {
                var resposta = new HttpResponseMessage(status);
                if (xml is not null)
                {
                    resposta.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
                }
                else if (conteudo is not null)
                {
                    resposta.Content = new ByteArrayContent(conteudo);
                    resposta.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(tipo!);
                }

                return resposta;
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FalharRede)
            {
                throw new HttpRequestException("Conexão recusada.");
            }

            Requisicoes.Add(new RequisicaoS3(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : null,
                request.Headers.TryGetValues("x-amz-content-sha256", out var hash) ? hash.Single() : null,
                request.Content?.Headers.ContentMD5 is { } md5 ? Convert.ToBase64String(md5) : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return _respostas.Dequeue()();
        }
    }
}
