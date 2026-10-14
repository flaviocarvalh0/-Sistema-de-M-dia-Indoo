using System.Net;
using Atlas.Application.Business.Conteudo.Common;
using Atlas.Infrastructure.Business.Conteudo;
using FluentAssertions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Conteudo;

/// <summary>Limite de tamanho do download de conteúdo externo.</summary>
public class ClienteConteudoExternoTests
{
    private static ClienteConteudoExterno Cliente(int tamanho, bool informarTamanho) =>
        new(new HttpClient(new Resposta(tamanho, informarTamanho)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AcimaDoLimite_Falha(bool informarTamanho)
    {
        var acao = () => Cliente(5000, informarTamanho).BaixarAsync(new Uri("https://a.test/x"), 1000, CancellationToken.None);

        await acao.Should().ThrowAsync<ConteudoExternoIndisponivelException>().WithMessage("*limite*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Truncando_DevolveOComeco(bool informarTamanho)
    {
        var conteudo = await Cliente(5000, informarTamanho).BaixarAsync(new Uri("https://a.test/x"), 1000, CancellationToken.None, truncar: true);

        conteudo.Bytes.Should().HaveCount(1000).And.OnlyContain(b => b == 7);
    }

    [Fact]
    public async Task EnderecoQueNaoEHttp_Falha()
    {
        var acao = () => Cliente(10, true).BaixarAsync(new Uri("file:///etc/passwd"), 1000, CancellationToken.None);

        await acao.Should().ThrowAsync<ConteudoExternoIndisponivelException>();
    }

    private sealed class Resposta(int tamanho, bool informarTamanho) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = new StreamContent(new MemoryStream(Enumerable.Repeat((byte)7, tamanho).ToArray()));
            if (!informarTamanho)
            {
                corpo.Headers.ContentLength = null;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = corpo });
        }
    }
}
