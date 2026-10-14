using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Application.Business.Conteudo.Noticias;

/// <summary>
/// Foto de destaque da página da notícia, para quando o feed não traz imagem:
/// as metatags <c>og:image</c> (Open Graph, a imagem que aparece ao
/// compartilhar o link) ou <c>twitter:image</c>. Lê só o começo da página.
/// </summary>
public static partial class PaginaNoticia
{
    /// <summary>As metatags ficam no &lt;head&gt;: não é preciso a página inteira.</summary>
    public const long TamanhoMaximoBytes = 1024 * 1024;

    public static Uri? ImagemDestaque(byte[] html, Uri pagina)
    {
        var texto = Encoding.UTF8.GetString(html);

        foreach (var nome in (string[])["og:image:secure_url", "og:image", "og:image:url", "twitter:image", "twitter:image:src"])
        {
            foreach (Match meta in MetaTag().Matches(texto))
            {
                var tag = meta.Value;
                var chave = Atributo(tag, "property") ?? Atributo(tag, "name");
                if (!string.Equals(chave, nome, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var conteudo = Atributo(tag, "content");
                if (!string.IsNullOrWhiteSpace(conteudo)
                    && Uri.TryCreate(pagina, WebUtility.HtmlDecode(conteudo.Trim()), out var url)
                    && url.Scheme is "http" or "https")
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static string? Atributo(string tag, string nome)
    {
        var m = Regex.Match(tag, $@"\b{nome}\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : null;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTag();
}
