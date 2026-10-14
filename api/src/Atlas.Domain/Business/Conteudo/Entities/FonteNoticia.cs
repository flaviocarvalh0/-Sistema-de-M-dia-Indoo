using Atlas.Domain.Platform.Attributes;

namespace Atlas.Domain.Business.Conteudo.Entities;

/// <summary>
/// Feed RSS ou Atom de notícias, cadastrado pela operadora e lido pela API a
/// cada hora. As notícias valem para toda a rede: cada playlist decide se
/// intercala notícias e com que frequência.
/// </summary>
/// <remarks>
/// A licença do conteúdo é responsabilidade de quem cadastra a fonte. A
/// fonte padrão é a Agência Brasil (Creative Commons BY), que exige o crédito
/// exibido junto da notícia (<see cref="Credito"/>).
/// </remarks>
[MetadataConfiguration("Fontes de notícias")]
public class FonteNoticia : BaseAuditableEntity
{
    /// <summary>Quantas notícias recentes ficam guardadas por fonte (com as imagens).</summary>
    public const int NoticiasGuardadas = 30;

    public string Nome { get; set; } = null!;

    /// <summary>Endereço https do feed.</summary>
    public string UrlFeed { get; set; } = null!;

    /// <summary>Texto exibido na TV junto da notícia (ex.: "Fonte: Agência Brasil").</summary>
    public string Credito { get; set; } = null!;

    public bool Ativo { get; set; } = true;

    /// <summary>Última leitura do feed, com ou sem erro.</summary>
    public DateTimeOffset? UltimaColetaEm { get; set; }

    /// <summary>Erro da última leitura; nulo quando ela deu certo.</summary>
    public string? UltimoErro { get; set; }

    /// <summary>Registra o resultado de uma leitura do feed.</summary>
    public void RegistrarColeta(DateTimeOffset quando, string? erro)
    {
        UltimaColetaEm = quando;
        UltimoErro = erro is { Length: > 500 } ? erro[..500] : erro;
    }
}
