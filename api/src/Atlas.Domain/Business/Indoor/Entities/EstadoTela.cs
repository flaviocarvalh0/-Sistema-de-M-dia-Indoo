namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Telemetria da TV, gravada a cada sincronização. Separada de <see cref="Tela"/>
/// para não encher o histórico da tela com uma versão a cada poucos minutos.
/// A chave é o id da tela (1:1).
/// </summary>
public class EstadoTela
{
    public long TelaId { get; set; }

    public DateTimeOffset UltimaSincronizacao { get; set; }

    /// <summary>Versão do app player informada pela TV.</summary>
    public string? VersaoApp { get; set; }

    /// <summary>Hash da grade entregue na última sincronização (ETag).</summary>
    public string? VersaoGrade { get; set; }

    /// <summary>Quantos itens a TV disse ter prontos no disco.</summary>
    public int? ItensProntos { get; set; }

    /// <summary>
    /// Quando a operadora foi avisada de que a TV parou de sincronizar; nulo =
    /// sem alerta em aberto. Limpo (com aviso de volta) quando ela sincroniza de novo.
    /// </summary>
    public DateTimeOffset? AlertaOfflineEm { get; set; }

    public void RegistrarSincronizacao(DateTimeOffset agora, string? versaoApp, string versaoGrade, int? itensProntos)
    {
        UltimaSincronizacao = agora;
        VersaoApp = versaoApp;
        VersaoGrade = versaoGrade;
        ItensProntos = itensProntos;
    }
}
