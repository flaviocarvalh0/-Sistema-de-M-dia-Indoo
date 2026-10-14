namespace Atlas.Domain.Business.Conteudo.Entities;

/// <summary>
/// Tempo agora na cidade de um parceiro que hospeda telas (o estabelecimento),
/// lido do Open-Meteo pela API a cada 30 minutos. A TV recebe na
/// sincronização e mostra o último valor que tiver, mesmo offline.
/// </summary>
public class PrevisaoClima : BaseEntity
{
    /// <summary>Previsão mais velha que isto não vai para as TVs.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(6);

    public long ParceiroId { get; set; }

    /// <summary>A cidade do parceiro quando as coordenadas foram buscadas: se ela muda, busca de novo.</summary>
    public string CidadeConsultada { get; set; } = null!;

    /// <summary>Nome do lugar encontrado (ex.: "Manaus, Amazonas"); nulo se a cidade não foi achada.</summary>
    public string? Local { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public decimal? TemperaturaC { get; set; }
    public decimal? MinimaC { get; set; }
    public decimal? MaximaC { get; set; }

    /// <summary>Código WMO do tempo (0 céu limpo … 99 tempestade com granizo).</summary>
    public int? CodigoTempo { get; set; }

    public DateTimeOffset? AtualizadaEm { get; set; }

    /// <summary>
    /// Previsão de hoje e dos próximos dias (cartão "Previsão do tempo"), em
    /// JSON: <c>[{"data":"2026-10-14","minimaC":24.1,"maximaC":33.9,"codigo":80}]</c>.
    /// </summary>
    public string? PrevisaoDias { get; set; }

    public string? UltimoErro { get; set; }
}
