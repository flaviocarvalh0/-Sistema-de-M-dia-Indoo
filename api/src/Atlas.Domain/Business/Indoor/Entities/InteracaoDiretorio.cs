using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Um acesso ou um toque na página do diretório de uma TV (ADR-027). Como as
/// leituras do QR (ADR-023), não guarda IP nem nada que identifique a pessoa:
/// <see cref="Visitante"/> é um hash que muda todo dia.
/// </summary>
/// <remarks>
/// Sem chave estrangeira: a contagem sobrevive à exclusão da TV ou do cliente.
/// </remarks>
public class InteracaoDiretorio : BaseEntity
{
    public long TelaId { get; set; }
    public long ParceiroId { get; set; }
    public EventoDiretorio Evento { get; set; }

    /// <summary>De quem é o contato tocado; só em <see cref="EventoDiretorio.Contato"/>.</summary>
    public long? ClienteId { get; set; }

    /// <summary>Qual contato (WhatsApp, Instagram…); só em <see cref="EventoDiretorio.Contato"/>.</summary>
    public TipoQrCode? Contato { get; set; }

    public DateTimeOffset Momento { get; set; }

    /// <summary>Hash diário (16 hex) de IP + navegador + data; não reversível.</summary>
    public string Visitante { get; set; } = null!;
}
