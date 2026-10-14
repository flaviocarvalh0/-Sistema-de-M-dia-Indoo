namespace Atlas.Domain.Business.Conteudo.Entities;

/// <summary>
/// Notícia lida de uma <see cref="FonteNoticia"/>. A imagem é baixada pela API
/// e guardada no storage, com hash, para a TV baixar e exibir offline como
/// faz com as mídias. Vem do feed ou, sem ela, da foto de destaque da página
/// da notícia (<c>og:image</c>). Sem imagem, a TV mostra só o título.
/// </summary>
public class Noticia : BaseEntity
{
    /// <summary>Notícia mais velha que isto não vai para as TVs.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(48);

    public const long TamanhoMaximoImagemBytes = 5L * 1024 * 1024;

    public long FonteNoticiaId { get; set; }
    public FonteNoticia Fonte { get; set; } = null!;

    /// <summary><c>guid</c> do item no feed (ou o link): evita guardar a mesma notícia duas vezes.</summary>
    public string Identificador { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    /// <summary>Data do feed; sem ela, a data da coleta.</summary>
    public DateTimeOffset PublicadaEm { get; set; }

    public DateTimeOffset ColetadaEm { get; set; }

    /// <summary>Caminho da imagem no bucket (<c>noticias/{fonteId}/{hash}.jpg</c>); nulo sem imagem.</summary>
    public string? ChaveImagem { get; set; }

    public string? TipoConteudoImagem { get; set; }

    public long? TamanhoImagemBytes { get; set; }

    /// <summary>SHA-256 da imagem, em hexadecimal minúsculo.</summary>
    public string? HashImagem { get; set; }
}
