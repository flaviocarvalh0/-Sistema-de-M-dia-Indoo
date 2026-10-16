using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Armazenamento;

/// <summary>
/// Formatos aceitos nas TVs: MP4 (H.264 toca em qualquer TV box Android),
/// JPEG e PNG. O tipo vale pelos primeiros bytes do arquivo, não pelo que o
/// navegador declarou.
/// </summary>
public static class TiposArquivoMidia
{
    public const string Mp4 = "video/mp4";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    /// <summary>Só para as fotos das notícias (muitos portais usam WebP); mídias não aceitam.</summary>
    public const string Webp = "image/webp";

    private static readonly Dictionary<string, (TipoMidia Tipo, string Extensao)> Aceitos = new()
    {
        [Mp4] = (TipoMidia.Video, ".mp4"),
        [Jpeg] = (TipoMidia.Imagem, ".jpg"),
        [Png] = (TipoMidia.Imagem, ".png"),
    };

    /// <summary>Quantos bytes do começo do arquivo bastam para <see cref="Detectar"/>.</summary>
    public const int BytesAssinatura = 12;

    public static IReadOnlyCollection<string> Todos => Aceitos.Keys;

    /// <summary>O APK do app das TVs (atualização automática, ADR-021).</summary>
    public const string Apk = "application/vnd.android.package-archive";

    /// <summary>O que o bucket aceita: os tipos das mídias, o WebP das fotos de notícias e o APK das TVs.</summary>
    public static IReadOnlyCollection<string> NoBucket => [.. Aceitos.Keys, Webp, Apk];

    /// <summary>JPEG, PNG ou WebP pela assinatura (fotos das notícias); nulo para o resto.</summary>
    public static string? DetectarImagem(ReadOnlySpan<byte> inicio)
    {
        if (Detectar(inicio) is Jpeg or Png)
        {
            return Detectar(inicio);
        }

        // RIFF <tamanho> WEBP
        return inicio.Length >= 12 && inicio[..4].SequenceEqual("RIFF"u8) && inicio[8..12].SequenceEqual("WEBP"u8)
            ? Webp
            : null;
    }

    public static string ExtensaoImagem(string tipoConteudo) => tipoConteudo == Webp ? ".webp" : Extensao(tipoConteudo);

    public static bool Aceito(string? tipoConteudo) => tipoConteudo is not null && Aceitos.ContainsKey(tipoConteudo);

    public static TipoMidia TipoDaMidia(string tipoConteudo) => Aceitos[tipoConteudo].Tipo;

    public static string Extensao(string tipoConteudo) => Aceitos[tipoConteudo].Extensao;

    /// <summary>MP4, JPEG ou PNG pela assinatura do arquivo; nulo para qualquer outra coisa.</summary>
    public static string? Detectar(ReadOnlySpan<byte> inicio)
    {
        // MP4/ISO BMFF: tamanho da caixa (4 bytes) e "ftyp".
        if (inicio.Length >= 8 && inicio[4..8].SequenceEqual("ftyp"u8))
        {
            return Mp4;
        }

        if (inicio.Length >= 3 && inicio[0] == 0xFF && inicio[1] == 0xD8 && inicio[2] == 0xFF)
        {
            return Jpeg;
        }

        if (inicio.Length >= 8 && inicio[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        return null;
    }
}
