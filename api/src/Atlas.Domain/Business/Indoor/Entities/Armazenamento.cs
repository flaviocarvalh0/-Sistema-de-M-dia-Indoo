namespace Atlas.Domain.Business.Indoor.Entities;

/// <summary>
/// Espaço reservado para um envio em andamento (ADR-025): a URL de envio só
/// sai se o arquivo couber no teto da rede contando também os envios que
/// ainda não terminaram. Some quando o arquivo é confirmado ou a URL expira.
/// </summary>
public class ReservaEnvio : BaseEntity
{
    public string Chave { get; set; } = null!;
    public long TamanhoBytes { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset ExpiraEm { get; set; }
}

/// <summary>
/// Resultado do último inventário do bucket (uma linha só, id 1): o uso REAL
/// do storage, medido listando os arquivos, e o nível de alerta já avisado.
/// </summary>
public class InventarioArmazenamento : BaseEntity
{
    public const long IdUnico = 1;

    public DateTimeOffset VerificadoEm { get; set; }

    /// <summary>Bytes no bucket na listagem (depois de apagar os órfãos); sem listagem, a soma do banco.</summary>
    public long BytesNoBucket { get; set; }

    /// <summary>Arquivos no bucket. Nulo: o storage não lista (Supabase) e não houve medição real.</summary>
    public int? Objetos { get; set; }

    /// <summary>Arquivos sem dono apagados nesta verificação (envios abandonados).</summary>
    public int OrfaosRemovidos { get; set; }
    public long BytesOrfaosRemovidos { get; set; }

    /// <summary>Último nível de alerta enviado (0, 70, 85 ou 95%); volta a 0 quando o uso cai.</summary>
    public int NivelAlertaAvisado { get; set; }
}
