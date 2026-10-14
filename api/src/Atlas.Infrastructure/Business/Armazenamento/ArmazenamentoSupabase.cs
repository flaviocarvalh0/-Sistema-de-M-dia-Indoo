using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// <see cref="IArmazenamentoMidias"/> sobre a Storage API do Supabase, por HTTP
/// com a chave <c>service_role</c> (bucket privado, sem políticas de RLS).
/// </summary>
/// <remarks>
/// Rotas usadas (relativas a <c>/storage/v1</c>):
/// <list type="bullet">
/// <item><c>POST object/upload/sign/{bucket}/{chave}</c> → <c>{ url, token }</c> (envio, validade fixa do Supabase: 2 h)</item>
/// <item><c>POST object/sign/{bucket}/{chave}</c> com <c>{ expiresIn }</c> → <c>{ signedURL }</c> (leitura)</item>
/// <item><c>POST object/sign/{bucket}</c> com <c>{ expiresIn, paths }</c> → <c>[{ path, signedURL, error }]</c> (leitura em lote)</item>
/// <item><c>HEAD object/authenticated/{bucket}/{chave}</c> (metadados)</item>
/// <item><c>GET object/authenticated/{bucket}/{chave}</c> (conteúdo)</item>
/// <item><c>POST object/{bucket}/{chave}</c> com <c>x-upsert: true</c> (gravação pela própria API)</item>
/// <item><c>DELETE object/{bucket}/{chave}</c></item>
/// <item><c>GET bucket/{bucket}</c>, <c>POST bucket</c> e <c>PUT bucket/{bucket}</c> (criação e tipos aceitos)</item>
/// </list>
/// </remarks>
internal sealed class ArmazenamentoSupabase(
    HttpClient http,
    OpcoesSupabase opcoes,
    TimeProvider timeProvider,
    ILogger<ArmazenamentoSupabase> logger) : IArmazenamentoMidias
{
    /// <summary>Validade das URLs de envio assinadas pelo Supabase (padrão do serviço).</summary>
    public static readonly TimeSpan ValidadeEnvio = TimeSpan.FromHours(2);

    public async Task<EnvioPreparado> PrepararEnvioAsync(string chave, CancellationToken cancellationToken)
    {
        var resposta = await EnviarAsync(HttpMethod.Post, $"object/upload/sign/{Caminho(chave)}", corpo: null, cancellationToken);
        var assinatura = await LerJsonAsync<RespostaUrlAssinada>(resposta, "preparar o envio", cancellationToken);

        return new EnvioPreparado(Absoluta(assinatura.Url ?? assinatura.SignedUrl), timeProvider.GetUtcNow() + ValidadeEnvio);
    }

    public async Task<Uri> GerarUrlLeituraAsync(string chave, TimeSpan validade, CancellationToken cancellationToken)
    {
        var corpo = Json(new { expiresIn = (int)Math.Ceiling(validade.TotalSeconds) });
        var resposta = await EnviarAsync(HttpMethod.Post, $"object/sign/{Caminho(chave)}", corpo, cancellationToken);
        var assinatura = await LerJsonAsync<RespostaUrlAssinada>(resposta, "gerar a URL de leitura", cancellationToken);

        return Absoluta(assinatura.SignedUrl ?? assinatura.Url);
    }

    public async Task<IReadOnlyDictionary<string, Uri>> GerarUrlsLeituraAsync(
        IReadOnlyCollection<string> chaves,
        TimeSpan validade,
        CancellationToken cancellationToken)
    {
        if (chaves.Count == 0)
        {
            return new Dictionary<string, Uri>();
        }

        var corpo = Json(new { expiresIn = (int)Math.Ceiling(validade.TotalSeconds), paths = chaves });
        var resposta = await EnviarAsync(HttpMethod.Post, $"object/sign/{Uri.EscapeDataString(opcoes.Bucket)}", corpo, cancellationToken);
        var assinaturas = await LerJsonAsync<List<RespostaUrlEmLote>>(resposta, "gerar as URLs de leitura", cancellationToken);

        return assinaturas
            .Where(a => a.Path is not null && !string.IsNullOrWhiteSpace(a.SignedUrl))
            .ToDictionary(a => a.Path!, a => Absoluta(a.SignedUrl));
    }

    public async Task<ObjetoArmazenado?> ObterAsync(string chave, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(HttpMethod.Head, $"object/authenticated/{Caminho(chave)}", corpo: null, cancellationToken);

        if (NaoEncontrado(resposta))
        {
            return null;
        }

        GarantirSucesso(resposta, "consultar o arquivo");

        return new ObjetoArmazenado(
            resposta.Content.Headers.ContentLength ?? 0,
            resposta.Content.Headers.ContentType?.MediaType);
    }

    public async Task<Stream?> AbrirLeituraAsync(string chave, CancellationToken cancellationToken)
    {
        var resposta = await EnviarAsync(HttpMethod.Get, $"object/authenticated/{Caminho(chave)}", corpo: null, cancellationToken);

        if (NaoEncontrado(resposta))
        {
            resposta.Dispose();
            return null;
        }

        GarantirSucesso(resposta, "ler o arquivo");

        return await resposta.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task GravarAsync(string chave, byte[] conteudo, string tipoConteudo, CancellationToken cancellationToken)
    {
        var corpo = new ByteArrayContent(conteudo);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(tipoConteudo);
        corpo.Headers.Add("x-upsert", "true");

        using var resposta = await EnviarAsync(HttpMethod.Post, $"object/{Caminho(chave)}", corpo, cancellationToken);
        GarantirSucesso(resposta, "gravar o arquivo");
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(HttpMethod.Delete, $"object/{Caminho(chave)}", corpo: null, cancellationToken);

        if (!NaoEncontrado(resposta))
        {
            GarantirSucesso(resposta, "remover o arquivo");
        }
    }

    /// <summary>
    /// Cria o bucket privado com os limites do produto, se ele ainda não
    /// existe. Existindo, acrescenta os tipos que faltarem (o WebP das fotos
    /// de notícias entrou depois da criação dos primeiros buckets).
    /// </summary>
    public async Task GarantirBucketAsync(CancellationToken cancellationToken)
    {
        using (var existente = await EnviarAsync(HttpMethod.Get, $"bucket/{Uri.EscapeDataString(opcoes.Bucket)}", corpo: null, cancellationToken))
        {
            if (existente.IsSuccessStatusCode)
            {
                var bucket = await existente.Content.ReadFromJsonAsync<RespostaBucket>(cancellationToken);
                var tipos = bucket?.AllowedMimeTypes;

                // Sem lista, o bucket aceita qualquer tipo: nada a fazer.
                if (tipos is null || TiposArquivoMidia.NoBucket.All(tipos.Contains))
                {
                    return;
                }

                using var atualizado = await EnviarAsync(HttpMethod.Put, $"bucket/{Uri.EscapeDataString(opcoes.Bucket)}", Json(new
                {
                    @public = false,
                    file_size_limit = Midia.TamanhoMaximoBytes,
                    allowed_mime_types = TiposArquivoMidia.NoBucket,
                }), cancellationToken);
                GarantirSucesso(atualizado, "atualizar os tipos do bucket");

                logger.LogInformation("Bucket {Bucket}: tipos aceitos atualizados ({Tipos}).", opcoes.Bucket, string.Join(", ", TiposArquivoMidia.NoBucket));
                return;
            }

            if (!NaoEncontrado(existente))
            {
                GarantirSucesso(existente, "consultar o bucket");
            }
        }

        var corpo = Json(new
        {
            id = opcoes.Bucket,
            name = opcoes.Bucket,
            @public = false,
            file_size_limit = Midia.TamanhoMaximoBytes,
            allowed_mime_types = TiposArquivoMidia.NoBucket,
        });

        using var criado = await EnviarAsync(HttpMethod.Post, "bucket", corpo, cancellationToken);
        GarantirSucesso(criado, "criar o bucket");

        logger.LogInformation("Bucket {Bucket} criado no Supabase Storage.", opcoes.Bucket);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string rota, HttpContent? corpo, CancellationToken cancellationToken)
    {
        using var requisicao = new HttpRequestMessage(metodo, new Uri(opcoes.BaseStorage, rota)) { Content = corpo };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.ChaveServico);
        requisicao.Headers.Add("apikey", opcoes.ChaveServico);

        try
        {
            return await http.SendAsync(requisicao, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

    private async Task<T> LerJsonAsync<T>(HttpResponseMessage resposta, string operacao, CancellationToken cancellationToken)
    {
        using (resposta)
        {
            GarantirSucesso(resposta, operacao);

            return await resposta.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new ArmazenamentoIndisponivelException($"Resposta vazia do storage ao {operacao}.");
        }
    }

    private void GarantirSucesso(HttpResponseMessage resposta, string operacao)
    {
        if (resposta.IsSuccessStatusCode)
        {
            return;
        }

        // A chave de serviço nunca entra na mensagem; o corpo do erro do
        // Supabase também não (pode repetir a requisição).
        logger.LogError("Supabase Storage respondeu {Status} ao {Operacao}.", (int)resposta.StatusCode, operacao);

        throw new ArmazenamentoIndisponivelException($"O storage de mídias recusou a operação ({(int)resposta.StatusCode}) ao {operacao}.");
    }

    /// <summary>Corpo JSON com <c>Content-Length</c> (o <c>JsonContent</c> iria em chunked).</summary>
    private static StringContent Json(object valor) =>
        new(JsonSerializer.Serialize(valor), Encoding.UTF8, "application/json");

    /// <summary>
    /// O Supabase responde 400 com "Object not found" (e às vezes 404) para
    /// objeto ou bucket inexistente.
    /// </summary>
    private static bool NaoEncontrado(HttpResponseMessage resposta) =>
        resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest;

    /// <summary>As URLs assinadas voltam relativas a <c>/storage/v1</c>.</summary>
    private Uri Absoluta(string? relativa)
    {
        if (string.IsNullOrWhiteSpace(relativa))
        {
            throw new ArmazenamentoIndisponivelException("O storage não devolveu a URL assinada.");
        }

        return new Uri(opcoes.BaseStorage, relativa.TrimStart('/'));
    }

    /// <summary>Bucket e chave com cada segmento codificado (a chave é gerada pela API, mas não custa).</summary>
    private string Caminho(string chave) =>
        $"{Uri.EscapeDataString(opcoes.Bucket)}/{string.Join('/', chave.Split('/').Select(Uri.EscapeDataString))}";

    private sealed class RespostaBucket
    {
        [JsonPropertyName("allowed_mime_types")]
        public List<string>? AllowedMimeTypes { get; init; }
    }

    /// <summary>Item da assinatura em lote; <c>signedURL</c> vem nulo (com <c>error</c>) para chave inexistente.</summary>
    private sealed class RespostaUrlEmLote
    {
        [JsonPropertyName("path")]
        public string? Path { get; init; }

        [JsonPropertyName("signedURL")]
        public string? SignedUrl { get; init; }
    }

    /// <summary>O envio devolve <c>url</c>; a leitura devolve <c>signedURL</c>.</summary>
    private sealed class RespostaUrlAssinada
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("signedURL")]
        public string? SignedUrl { get; init; }
    }
}
