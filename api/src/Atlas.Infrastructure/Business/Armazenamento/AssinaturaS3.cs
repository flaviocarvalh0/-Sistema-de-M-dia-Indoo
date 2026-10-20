using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Business.Armazenamento;

/// <summary>
/// Assinatura AWS Signature Version 4 para a API S3, usada pelo Cloudflare R2
/// (região <c>auto</c>). Feita à mão, sem o SDK da AWS: o SDK novo manda
/// cabeçalhos de checksum que o R2 nem sempre aceita, e assim a assinatura é
/// testável com os exemplos oficiais da documentação da AWS.
/// </summary>
/// <remarks>
/// Duas formas: URL pré-assinada (<see cref="PreAssinar"/>, para o navegador
/// enviar e a TV baixar sem credencial) e cabeçalho <c>Authorization</c>
/// (<see cref="Assinar"/>, para a própria API).
/// </remarks>
internal sealed class AssinaturaS3(string chaveAcesso, string segredo, string regiao = "auto", string servico = "s3")
{
    public const string PayloadNaoAssinado = "UNSIGNED-PAYLOAD";
    public const string PayloadVazio = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>Validade máxima de uma URL pré-assinada no S3/R2 (7 dias).</summary>
    public static readonly TimeSpan ValidadeMaxima = TimeSpan.FromDays(7);

    /// <summary>
    /// URL pré-assinada. <paramref name="cabecalhos"/> são os cabeçalhos que o
    /// cliente É OBRIGADO a mandar com estes valores (ex.: <c>content-type</c> e
    /// <c>content-length</c> no envio): o storage recusa se não baterem.
    /// O <c>host</c> é incluído sempre.
    /// </summary>
    public Uri PreAssinar(
        string metodo,
        Uri endereco,
        DateTimeOffset agora,
        TimeSpan validade,
        IReadOnlyDictionary<string, string>? cabecalhos = null)
    {
        if (validade <= TimeSpan.Zero || validade > ValidadeMaxima)
        {
            throw new ArgumentOutOfRangeException(nameof(validade), "A validade deve ficar entre 1 s e 7 dias.");
        }

        var (data, dataHora) = Datas(agora);
        var escopo = $"{data}/{regiao}/{servico}/aws4_request";
        var assinados = Cabecalhos(endereco, cabecalhos);
        var nomesAssinados = string.Join(';', assinados.Keys);

        var consulta = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{chaveAcesso}/{escopo}",
            ["X-Amz-Date"] = dataHora,
            ["X-Amz-Expires"] = ((long)validade.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = nomesAssinados,
        };
        foreach (var (nome, valor) in ConsultaExistente(endereco))
        {
            consulta[nome] = valor;
        }

        var consultaCanonica = ConsultaCanonica(consulta);
        var requisicaoCanonica = string.Join('\n',
            metodo.ToUpperInvariant(),
            CaminhoCanonico(endereco),
            consultaCanonica,
            CabecalhosCanonicos(assinados),
            nomesAssinados,
            PayloadNaoAssinado);

        var assinatura = Assinatura(data, dataHora, escopo, requisicaoCanonica);
        var construtor = new UriBuilder(endereco) { Query = $"{consultaCanonica}&X-Amz-Signature={assinatura}" };
        return construtor.Uri;
    }

    /// <summary>
    /// Assina a requisição pelo cabeçalho <c>Authorization</c> (põe também
    /// <c>x-amz-date</c> e <c>x-amz-content-sha256</c>). Os cabeçalhos de conteúdo
    /// (<c>content-type</c>) entram na assinatura se estiverem no conteúdo.
    /// </summary>
    public void Assinar(HttpRequestMessage requisicao, DateTimeOffset agora, string hashPayload)
    {
        var endereco = requisicao.RequestUri ?? throw new InvalidOperationException("Requisição sem endereço.");
        var (data, dataHora) = Datas(agora);
        var escopo = $"{data}/{regiao}/{servico}/aws4_request";

        requisicao.Headers.Remove("x-amz-date");
        requisicao.Headers.Remove("x-amz-content-sha256");
        requisicao.Headers.TryAddWithoutValidation("x-amz-date", dataHora);
        requisicao.Headers.TryAddWithoutValidation("x-amz-content-sha256", hashPayload);

        var extras = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (nome, valores) in requisicao.Headers)
        {
            if (nome.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            extras[nome.ToLowerInvariant()] = string.Join(',', valores);
        }

        if (requisicao.Content?.Headers.ContentType is { } tipo)
        {
            extras["content-type"] = tipo.ToString();
        }

        if (requisicao.Content?.Headers.ContentMD5 is { } md5)
        {
            extras["content-md5"] = Convert.ToBase64String(md5);
        }

        var assinados = Cabecalhos(endereco, extras);
        var nomesAssinados = string.Join(';', assinados.Keys);
        var requisicaoCanonica = string.Join('\n',
            requisicao.Method.Method.ToUpperInvariant(),
            CaminhoCanonico(endereco),
            ConsultaCanonica(new SortedDictionary<string, string>(ConsultaExistente(endereco).ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal)),
            CabecalhosCanonicos(assinados),
            nomesAssinados,
            hashPayload);

        var assinatura = Assinatura(data, dataHora, escopo, requisicaoCanonica);
        requisicao.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={chaveAcesso}/{escopo},SignedHeaders={nomesAssinados},Signature={assinatura}");
    }

    public static string Sha256Hex(ReadOnlySpan<byte> conteudo) => Convert.ToHexStringLower(SHA256.HashData(conteudo));

    /// <summary>Codificação da AWS: só letras, números e <c>-_.~</c> ficam como estão.</summary>
    public static string Codificar(string texto, bool manterBarra = false)
    {
        var saida = new StringBuilder(texto.Length * 2);
        foreach (var b in Encoding.UTF8.GetBytes(texto))
        {
            var c = (char)b;
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.' or '~'
                || (manterBarra && c == '/'))
            {
                saida.Append(c);
            }
            else
            {
                saida.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return saida.ToString();
    }

    private string Assinatura(string data, string dataHora, string escopo, string requisicaoCanonica)
    {
        var textoParaAssinar = string.Join('\n',
            "AWS4-HMAC-SHA256",
            dataHora,
            escopo,
            Sha256Hex(Encoding.UTF8.GetBytes(requisicaoCanonica)));

        var chave = Hmac(Encoding.UTF8.GetBytes("AWS4" + segredo), data);
        chave = Hmac(chave, regiao);
        chave = Hmac(chave, servico);
        chave = Hmac(chave, "aws4_request");
        return Convert.ToHexStringLower(Hmac(chave, textoParaAssinar));
    }

    private static byte[] Hmac(byte[] chave, string texto) => HMACSHA256.HashData(chave, Encoding.UTF8.GetBytes(texto));

    private static (string Data, string DataHora) Datas(DateTimeOffset agora)
    {
        var utc = agora.UtcDateTime;
        return (utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture), utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
    }

    private static SortedDictionary<string, string> Cabecalhos(Uri endereco, IReadOnlyDictionary<string, string>? extras)
    {
        var cabecalhos = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = endereco.IsDefaultPort ? endereco.Host : $"{endereco.Host}:{endereco.Port}",
        };

        foreach (var (nome, valor) in extras ?? new Dictionary<string, string>())
        {
            cabecalhos[nome.ToLowerInvariant()] = valor;
        }

        return cabecalhos;
    }

    private static string CabecalhosCanonicos(SortedDictionary<string, string> cabecalhos) =>
        string.Concat(cabecalhos.Select(c => $"{c.Key}:{string.Join(' ', c.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))}\n"));

    /// <summary>O caminho já vem codificado no <see cref="Uri"/>: decodifica e recodifica no padrão da AWS.</summary>
    private static string CaminhoCanonico(Uri endereco)
    {
        var caminho = Uri.UnescapeDataString(endereco.AbsolutePath);
        return string.IsNullOrEmpty(caminho) ? "/" : Codificar(caminho, manterBarra: true);
    }

    private static IEnumerable<KeyValuePair<string, string>> ConsultaExistente(Uri endereco)
    {
        var consulta = endereco.Query.TrimStart('?');
        if (consulta.Length == 0)
        {
            yield break;
        }

        foreach (var par in consulta.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = par.IndexOf('=');
            yield return i < 0
                ? new(Uri.UnescapeDataString(par), string.Empty)
                : new(Uri.UnescapeDataString(par[..i]), Uri.UnescapeDataString(par[(i + 1)..]));
        }
    }

    private static string ConsultaCanonica(SortedDictionary<string, string> consulta) =>
        string.Join('&', consulta.Select(p => $"{Codificar(p.Key)}={Codificar(p.Value)}"));
}
