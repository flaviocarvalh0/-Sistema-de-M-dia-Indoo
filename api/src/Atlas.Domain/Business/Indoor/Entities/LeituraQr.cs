namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Uma leitura do QR code rastreado de um anúncio (<c>/q/{codigo}</c>, ADR-023).
/// Não guarda IP nem nada que identifique a pessoa: <see cref="Visitante"/> é
/// um hash que muda todo dia, só para contar leituras únicas por dia.
/// </summary>
public class LeituraQr : BaseEntity
{
    public const int TamanhoVisitante = 16;

    public long MidiaId { get; set; }
    public long ClienteId { get; set; }
    public DateTimeOffset Momento { get; set; }

    /// <summary>Hash diário (16 hex) de IP + navegador + data; não reversível.</summary>
    public string Visitante { get; set; } = null!;
}
