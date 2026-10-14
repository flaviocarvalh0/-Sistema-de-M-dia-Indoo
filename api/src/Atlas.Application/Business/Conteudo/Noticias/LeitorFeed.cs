using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Atlas.Application.Business.Conteudo.Noticias;

/// <summary>
/// Lê feeds RSS 2.0 e Atom sem dependência externa. Da notícia interessam o
/// identificador, o título, a data e uma imagem (enclosure, <c>media:content</c>,
/// <c>media:thumbnail</c> ou a primeira <c>&lt;img&gt;</c> do texto).
/// </summary>
/// <remarks>
/// O XML vem de fora: DTD ignorado e sem resolver entidades externas (XXE).
/// </remarks>
public static partial class LeitorFeed
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Conteudo = "http://purl.org/rss/1.0/modules/content/";

    /// <exception cref="FormatException">Não é um XML de feed RSS ou Atom.</exception>
    public static IReadOnlyList<ItemFeed> Ler(byte[] xml, Uri endereco)
    {
        XDocument documento;
        try
        {
            using var fluxo = new MemoryStream(xml);
            using var leitor = XmlReader.Create(fluxo, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024,
                IgnoreComments = true,
            });
            documento = XDocument.Load(leitor);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"O endereço não devolveu um XML válido ({ex.Message}).", ex);
        }

        var raiz = documento.Root ?? throw new FormatException("O feed está vazio.");

        if (raiz.Name.LocalName == "rss" || raiz.Name.LocalName == "RDF")
        {
            return raiz.Descendants().Where(e => e.Name.LocalName == "item").Select(i => LerItemRss(i, endereco)).OfType<ItemFeed>().ToList();
        }

        if (raiz.Name == Atom + "feed")
        {
            return raiz.Elements(Atom + "entry").Select(e => LerEntradaAtom(e, endereco)).OfType<ItemFeed>().ToList();
        }

        throw new FormatException("O endereço não é um feed RSS ou Atom.");
    }

    private static ItemFeed? LerItemRss(XElement item, Uri endereco)
    {
        var titulo = LimparTexto(Filho(item, "title"));
        var link = Filho(item, "link")?.Trim();
        var identificador = Filho(item, "guid")?.Trim() is { Length: > 0 } guid ? guid : link;

        if (string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(identificador))
        {
            return null;
        }

        var data = LerData(Filho(item, "pubDate") ?? Filho(item, "date") ?? Filho(item, "updated"));
        var baseUrl = Absoluta(link, endereco) ?? endereco;

        var imagem =
            item.Elements("enclosure").Where(e => EhImagem((string?)e.Attribute("type"), (string?)e.Attribute("url")))
                .Select(e => (string?)e.Attribute("url")).FirstOrDefault()
            ?? ImagemMedia(item)
            ?? PrimeiraImagem(item.Element(Conteudo + "encoded")?.Value)
            ?? PrimeiraImagem(Filho(item, "description"));

        return new ItemFeed(Cortar(identificador, 500), Cortar(titulo, 300), data, Absoluta(imagem, baseUrl), Absoluta(link, endereco));
    }

    private static ItemFeed? LerEntradaAtom(XElement entrada, Uri endereco)
    {
        var titulo = LimparTexto(entrada.Element(Atom + "title")?.Value);
        var link = entrada.Elements(Atom + "link")
            .FirstOrDefault(l => ((string?)l.Attribute("rel") ?? "alternate") == "alternate")?.Attribute("href")?.Value;
        var identificador = entrada.Element(Atom + "id")?.Value.Trim() is { Length: > 0 } id ? id : link;

        if (string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(identificador))
        {
            return null;
        }

        var data = LerData(entrada.Element(Atom + "published")?.Value ?? entrada.Element(Atom + "updated")?.Value);
        var baseUrl = Absoluta(link, endereco) ?? endereco;

        var imagem =
            entrada.Elements(Atom + "link")
                .Where(l => (string?)l.Attribute("rel") == "enclosure" && EhImagem((string?)l.Attribute("type"), (string?)l.Attribute("href")))
                .Select(l => (string?)l.Attribute("href")).FirstOrDefault()
            ?? ImagemMedia(entrada)
            ?? PrimeiraImagem(entrada.Element(Atom + "content")?.Value)
            ?? PrimeiraImagem(entrada.Element(Atom + "summary")?.Value);

        return new ItemFeed(Cortar(identificador, 500), Cortar(titulo, 300), data, Absoluta(imagem, baseUrl), Absoluta(link, endereco));
    }

    private static string? ImagemMedia(XElement item) =>
        item.Descendants(Media + "content")
            .Where(m => (string?)m.Attribute("medium") == "image" || EhImagem((string?)m.Attribute("type"), (string?)m.Attribute("url")))
            .Select(m => (string?)m.Attribute("url")).FirstOrDefault()
        ?? item.Descendants(Media + "thumbnail").Select(m => (string?)m.Attribute("url")).FirstOrDefault();

    private static bool EhImagem(string? tipo, string? url) =>
        tipo?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
        || (tipo is null && url is not null && ExtensaoImagem().IsMatch(url));

    private static string? PrimeiraImagem(string? html) =>
        string.IsNullOrEmpty(html) ? null : TagImagem().Match(html) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;

    /// <summary>Filho pelo nome local (o RSS 1.0 e o Dublin Core usam namespaces).</summary>
    private static string? Filho(XElement item, string nome) =>
        item.Elements().FirstOrDefault(e => e.Name.LocalName == nome)?.Value;

    private static Uri? Absoluta(string? url, Uri baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(baseUrl, url.Trim(), out var absoluta))
        {
            return null;
        }

        return absoluta.Scheme is "http" or "https" ? absoluta : null;
    }

    /// <summary>Sem HTML, com entidades decodificadas e espaços normalizados.</summary>
    public static string LimparTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var semTags = Tag().Replace(WebUtility.HtmlDecode(texto), " ");
        return Espacos().Replace(WebUtility.HtmlDecode(semTags), " ").Trim();
    }

    private static string Cortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";

    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["feb"] = 2, ["fev"] = 2, ["mar"] = 3, ["apr"] = 4, ["abr"] = 4, ["may"] = 5, ["mai"] = 5,
        ["jun"] = 6, ["jul"] = 7, ["aug"] = 8, ["ago"] = 8, ["sep"] = 9, ["set"] = 9, ["oct"] = 10, ["out"] = 10,
        ["nov"] = 11, ["dec"] = 12, ["dez"] = 12,
    };

    private static readonly Dictionary<string, int> Fusos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GMT"] = 0, ["UT"] = 0, ["UTC"] = 0, ["Z"] = 0,
        ["EST"] = -5, ["EDT"] = -4, ["CST"] = -6, ["CDT"] = -5, ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7,
        ["BRT"] = -3, ["BRST"] = -2, ["AMT"] = -4,
    };

    /// <summary>RFC 822 (RSS, inclusive com fuso por sigla) ou ISO 8601 (Atom); nulo se não entender.</summary>
    public static DateTimeOffset? LerData(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var m = DataRfc822().Match(texto.Trim());
        if (m.Success && Meses.TryGetValue(m.Groups["mes"].Value[..3], out var mes))
        {
            var ano = int.Parse(m.Groups["ano"].Value, CultureInfo.InvariantCulture);
            if (ano < 100)
            {
                ano += 2000;
            }

            var fuso = TimeSpan.Zero;
            var textoFuso = m.Groups["fuso"].Value;
            if (textoFuso.Length == 5 && (textoFuso[0] == '+' || textoFuso[0] == '-'))
            {
                var minutos = int.Parse(textoFuso[1..3], CultureInfo.InvariantCulture) * 60 + int.Parse(textoFuso[3..5], CultureInfo.InvariantCulture);
                fuso = TimeSpan.FromMinutes(textoFuso[0] == '-' ? -minutos : minutos);
            }
            else if (Fusos.TryGetValue(textoFuso, out var horas))
            {
                fuso = TimeSpan.FromHours(horas);
            }

            try
            {
                return new DateTimeOffset(
                    ano, mes, int.Parse(m.Groups["dia"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups["min"].Value, CultureInfo.InvariantCulture),
                    m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) : 0,
                    fuso);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return DateTimeOffset.TryParse(texto.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso)
            ? iso
            : null;
    }

    [GeneratedRegex(@"^(?:[A-Za-zÀ-ú]{3}[a-zà-ú]*,?\s+)?(?<dia>\d{1,2})\s+(?<mes>[A-Za-z]{3,})\.?\s+(?<ano>\d{2,4})\s+(?<h>\d{1,2}):(?<min>\d{2})(?::(?<s>\d{2}))?\s*(?<fuso>[+-]\d{4}|[A-Za-z]{1,5})?", RegexOptions.CultureInvariant)]
    private static partial Regex DataRfc822();

    [GeneratedRegex(@"<img\b[^>]*?\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TagImagem();

    [GeneratedRegex(@"\.(jpe?g|png)(\?|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExtensaoImagem();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();
}

/// <summary>
/// Item do feed: <c>Identificador</c> é o <c>guid</c>/<c>id</c> (ou o link);
/// <c>PublicadaEm</c> é nulo sem data legível; <c>Imagem</c> e <c>Link</c>
/// (a página da notícia) são absolutos, http(s).
/// </summary>
public sealed record ItemFeed(string Identificador, string Titulo, DateTimeOffset? PublicadaEm, Uri? Imagem, Uri? Link = null);
