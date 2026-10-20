using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Atlas.Application.Business.Indoor.Armazenamento;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// <see cref="IArmazenamentoMidias"/> sobre o Cloudflare R2 pela API S3 (ADR-025):
/// bucket privado, sem taxa de saída (as TVs baixam de graça), 10 GB grátis.
/// </summary>
/// <remarks>
/// O navegador e as TVs usam URLs pré-assinadas (sem credencial). No envio, o
/// <c>Content-Type</c> e o <c>Content-Length</c> vão na assinatura: o R2 recusa
/// um arquivo maior que o declarado — é uma das travas do teto de 10 GB
/// (<see cref="TetoArmazenamento"/>). As URLs de leitura são geradas aqui, sem
/// ir ao R2 (nenhuma operação cobrada).
/// </remarks>
internal sealed class ArmazenamentoR2(
    HttpClient http,
    OpcoesR2 opcoes,
    TimeProvider timeProvider,
    ILogger<ArmazenamentoR2> logger) : IArmazenamentoMidias, IListagemArmazenamento
{
    /// <summary>Validade das URLs de envio (a reserva de espaço dura o mesmo tempo).</summary>
    public static readonly TimeSpan ValidadeEnvio = TimeSpan.FromHours(2);

    private readonly AssinaturaS3 _assinatura = new(opcoes.ChaveAcesso, opcoes.SegredoAcesso);

    public Task<EnvioPreparado> PrepararEnvioAsync(string chave, string tipoConteudo, long tamanhoBytes, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();
        var url = _assinatura.PreAssinar("PUT", opcoes.Objeto(chave), agora, ValidadeEnvio, new Dictionary<string, string>
        {
            ["content-type"] = tipoConteudo,
            ["content-length"] = tamanhoBytes.ToString(CultureInfo.InvariantCulture),
        });

        return Task.FromResult(new EnvioPreparado(url, agora + ValidadeEnvio));
    }

    public Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken) =>
        Task.FromResult(Leitura(chave, validade));

    /// <summary>
    /// Sem ir ao R2: a chave que não existir dá 404 no download (a TV tenta de
    /// novo e o inventário avisa). Conferir cada uma custaria uma operação por item.
    /// </summary>
    public Task<IReadOnlyDictionary<string, Uri>> GerarUrlsLeituraAsync(
        IReadOnlyCollection<string> chaves,
        TimeSpan validade,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, Uri> urls = chaves.Distinct().ToDictionary(c => c, c => Leitura(c, validade));
        return Task.FromResult(urls);
    }

    public async Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(new HttpRequestMessage(HttpMethod.Head, opcoes.Objeto(chave)), cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        GarantirSucesso(resposta, "conferir o arquivo");
        return new ObjetoArmazenado(resposta.Content.Headers.ContentLength ?? 0, resposta.Content.Headers.ContentType?.MediaType);
    }

    public async Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken)
    {
        var resposta = await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, opcoes.Objeto(chave)), cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            resposta.Dispose();
            return null;
        }

        GarantirSucesso(resposta, "ler o arquivo");
        return await resposta.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task GravarAsync(string chave, byte[] conteudo, string tipoConteudo, CancellationToken cancellationToken)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Put, opcoes.Objeto(chave))
        {
            Content = new ByteArrayContent(conteudo),
        };
        requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue(tipoConteudo);

        using var resposta = await EnviarAsync(requisicao, cancellationToken, hashPayload: AssinaturaS3.Sha256Hex(conteudo));
        GarantirSucesso(resposta, "gravar o arquivo");
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(new HttpRequestMessage(HttpMethod.Delete, opcoes.Objeto(chave)), cancellationToken);
        if (resposta.StatusCode != HttpStatusCode.NotFound)
        {
            GarantirSucesso(resposta, "apagar o arquivo");
        }
    }

    /// <summary>ListObjectsV2, de 1.000 em 1.000 (uma operação classe A por página).</summary>
    public async IAsyncEnumerable<ObjetoListado> ListarAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuacao = null;
        do
        {
            var consulta = "list-type=2&max-keys=1000"
                + (continuacao is null ? string.Empty : "&continuation-token=" + AssinaturaS3.Codificar(continuacao));
            using var resposta = await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, opcoes.BucketUri(consulta)), cancellationToken);
            GarantirSucesso(resposta, "listar o bucket");

            var xml = XDocument.Parse(await resposta.Content.ReadAsStringAsync(cancellationToken));
            var ns = xml.Root!.Name.Namespace;
            foreach (var item in xml.Root.Elements(ns + "Contents"))
            {
                yield return new ObjetoListado(
                    item.Element(ns + "Key")!.Value,
                    long.Parse(item.Element(ns + "Size")!.Value, CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(item.Element(ns + "LastModified")!.Value, CultureInfo.InvariantCulture));
            }

            continuacao = string.Equals(xml.Root.Element(ns + "IsTruncated")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                ? xml.Root.Element(ns + "NextContinuationToken")?.Value
                : null;
        }
        while (continuacao is not null);
    }

    /// <summary>
    /// Libera o envio direto do navegador (PUT) a partir do painel. Precisa de
    /// um token com permissão de administrar o bucket; sem ela, o CORS é
    /// configurado no painel da Cloudflare (ver docs/IMPLANTACAO.md).
    /// </summary>
    public async Task ConfigurarCorsAsync(IReadOnlyCollection<string> origens, CancellationToken cancellationToken)
    {
        var regras = new XElement("CORSConfiguration",
            new XElement("CORSRule",
                origens.Select(o => new XElement("AllowedOrigin", o.TrimEnd('/'))),
                new XElement("AllowedMethod", "PUT"),
                new XElement("AllowedMethod", "GET"),
                new XElement("AllowedMethod", "HEAD"),
                new XElement("AllowedHeader", "*"),
                new XElement("MaxAgeSeconds", 3600)));
        var corpo = Encoding.UTF8.GetBytes(regras.ToString(SaveOptions.DisableFormatting));

        var requisicao = new HttpRequestMessage(HttpMethod.Put, opcoes.BucketUri("cors=")) { Content = new ByteArrayContent(corpo) };
        requisicao.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        requisicao.Content.Headers.ContentMD5 = MD5.HashData(corpo);

        using var resposta = await EnviarAsync(requisicao, cancellationToken, hashPayload: AssinaturaS3.Sha256Hex(corpo));
        GarantirSucesso(resposta, "configurar o CORS do bucket");
    }

    private Uri Leitura(string chave, TimeSpan validade) =>
        _assinatura.PreAssinar("GET", opcoes.Objeto(chave), timeProvider.GetUtcNow(), validade > AssinaturaS3.ValidadeMaxima ? AssinaturaS3.ValidadeMaxima : validade);

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpRequestMessage requisicao,
        CancellationToken cancellationToken,
        HttpCompletionOption completar = HttpCompletionOption.ResponseContentRead,
        string hashPayload = AssinaturaS3.PayloadVazio)
    {
        _assinatura.Assinar(requisicao, timeProvider.GetUtcNow(), hashPayload);
        try
        {
            return await http.SendAsync(requisicao, completar, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ArmazenamentoIndisponivelException("O storage de mídias não respondeu. Tente novamente em instantes.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArmazenamentoIndisponivelException("O storage de mídias demorou demais para responder. Tente novamente.", ex);
        }
    }

    private void GarantirSucesso(HttpResponseMessage resposta, string operacao)
    {
        if (resposta.IsSuccessStatusCode)
        {
            return;
        }

        logger.LogWarning("R2 respondeu {Status} ao {Operacao}.", (int)resposta.StatusCode, operacao);
        throw new ArmazenamentoIndisponivelException($"O storage de mídias recusou a operação ({(int)resposta.StatusCode}) ao {operacao}.");
    }
}
