using Atlas.Infrastructure.Business.Armazenamento;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>
/// Assinatura SigV4 conferida com os exemplos oficiais da documentação do S3
/// ("Authenticating Requests: Using Query Parameters" e "Using the
/// Authorization Header", credenciais de exemplo da AWS).
/// </summary>
public class AssinaturaS3Tests
{
    private static readonly AssinaturaS3 Aws = new("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1");
    private static readonly DateTimeOffset Data = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UrlPreAssinada_ExemploOficialDaAws()
    {
        var url = Aws.PreAssinar("GET", new Uri("https://examplebucket.s3.amazonaws.com/test.txt"), Data, TimeSpan.FromSeconds(86400));

        url.AbsoluteUri.Should().Be(
            "https://examplebucket.s3.amazonaws.com/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256"
            + "&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request"
            + "&X-Amz-Date=20130524T000000Z&X-Amz-Expires=86400&X-Amz-SignedHeaders=host"
            + "&X-Amz-Signature=aeeed9bbccd4d02ee5c0109b86d86835f995330da4c265957d157751f604d404");
    }

    [Fact]
    public void CabecalhoAuthorization_ExemploOficialDaAws_GetComRange()
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "https://examplebucket.s3.amazonaws.com/test.txt");
        requisicao.Headers.TryAddWithoutValidation("Range", "bytes=0-9");

        Aws.Assinar(requisicao, Data, AssinaturaS3.PayloadVazio);

        requisicao.Headers.GetValues("Authorization").Single().Should().Be(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
            + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,"
            + "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41");
    }

    [Fact]
    public void Envio_AssinaTipoETamanho_NaUrl()
    {
        var r2 = new AssinaturaS3("chave", "segredo");
        var url = r2.PreAssinar("PUT", new Uri("https://conta.r2.cloudflarestorage.com/midias/clientes/1/a b.mp4"), Data, TimeSpan.FromHours(1),
            new Dictionary<string, string> { ["content-type"] = "video/mp4", ["content-length"] = "1000" });

        url.AbsolutePath.Should().Be("/midias/clientes/1/a%20b.mp4");
        url.Query.Should().Contain("X-Amz-SignedHeaders=content-length%3Bcontent-type%3Bhost");
        url.Query.Should().Contain("%2Fauto%2Fs3%2Faws4_request");

        // Outro tamanho = outra assinatura: o storage recusa um arquivo maior que o declarado.
        var outra = r2.PreAssinar("PUT", new Uri("https://conta.r2.cloudflarestorage.com/midias/clientes/1/a b.mp4"), Data, TimeSpan.FromHours(1),
            new Dictionary<string, string> { ["content-type"] = "video/mp4", ["content-length"] = "1001" });
        outra.Should().NotBe(url);
    }

    [Fact]
    public void Validade_AteSeteDias()
    {
        var acao = () => Aws.PreAssinar("GET", new Uri("https://x.test/a"), Data, TimeSpan.FromDays(8));
        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("abc-_.~", "abc-_.~")]
    [InlineData("a b/c", "a%20b%2Fc")]
    [InlineData("ç", "%C3%A7")]
    public void Codificacao_DaAws(string texto, string esperado) => AssinaturaS3.Codificar(texto).Should().Be(esperado);
}
