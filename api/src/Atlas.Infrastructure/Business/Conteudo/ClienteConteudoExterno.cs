using System.Net.Http.Headers;
using Atlas.Application.Business.Conteudo.Common;

namespace Atlas.Infrastructure.Business.Conteudo;

/// <summary>
/// GET com limite de tamanho (lido em blocos, sem confiar no Content-Length)
/// e de tempo. Segue redirecionamentos só para http(s).
/// </summary>
internal sealed class ClienteConteudoExterno(HttpClient http) : IClienteConteudoExterno
{
    public async Task<ConteudoBaixado> BaixarAsync(Uri url, long limiteBytes, CancellationToken cancellationToken)
    {
        if (url.Scheme is not ("http" or "https"))
        {
            throw new ConteudoExternoIndisponivelException($"Endereço não suportado: {url}.");
        }

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
            using var resposta = await http.SendAsync(requisicao, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                throw new ConteudoExternoIndisponivelException($"{url.Host} respondeu {(int)resposta.StatusCode} ({resposta.ReasonPhrase}).");
            }

            if (resposta.Content.Headers.ContentLength > limiteBytes)
            {
                throw new ConteudoExternoIndisponivelException($"Conteúdo de {url.Host} maior que o limite ({limiteBytes / 1024} KB).");
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            using var destino = new MemoryStream();
            var buffer = new byte[81920];
            int lidos;
            while ((lidos = await fluxo.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (destino.Length + lidos > limiteBytes)
                {
                    throw new ConteudoExternoIndisponivelException($"Conteúdo de {url.Host} maior que o limite ({limiteBytes / 1024} KB).");
                }

                destino.Write(buffer, 0, lidos);
            }

            return new ConteudoBaixado(destino.ToArray(), resposta.Content.Headers.ContentType?.MediaType);
        }
        catch (HttpRequestException ex)
        {
            throw new ConteudoExternoIndisponivelException($"{url.Host} não respondeu ({ex.Message}).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConteudoExternoIndisponivelException($"{url.Host} demorou demais para responder.", ex);
        }
    }

    public static HttpClient CriarHttpClient()
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        // Alguns portais recusam (403) leitores sem um User-Agent no formato dos navegadores.
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "Mozilla/5.0 (compatible; MidiaIndoor/1.0; leitor de RSS)");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        return http;
    }
}
