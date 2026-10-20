using Atlas.Domain.Business.Indoor.Enums;

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

    /// <summary>O <c>versionCode</c> do app na TV (atualização automática, ADR-021); nulo em app antigo.</summary>
    public int? VersaoCodigoApp { get; set; }

    /// <summary>Hash da grade entregue na última sincronização (ETag).</summary>
    public string? VersaoGrade { get; set; }

    /// <summary>Quantos itens a TV disse ter prontos no disco.</summary>
    public int? ItensProntos { get; set; }

    /// <summary>
    /// Quando a operadora foi avisada de que a TV parou de sincronizar; nulo =
    /// sem alerta em aberto. Limpo (com aviso de volta) quando ela sincroniza de novo.
    /// </summary>
    public DateTimeOffset? AlertaOfflineEm { get; set; }

    // ── Heartbeat de reprodução (ADR-024) ───────────────────────────────

    /// <summary>Item na tela além do tempo dele por mais que isto = travado.</summary>
    public static readonly TimeSpan FolgaTravamento = TimeSpan.FromMinutes(2);

    /// <summary>Relógio da TV diferente do servidor por mais que isto = relógio errado.</summary>
    public static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    public ModoReproducao? ModoReproducao { get; set; }

    /// <summary>Desde quando o item atual (ou a espera) está na tela, pelo relógio da TV.</summary>
    public DateTimeOffset? ReproducaoDesde { get; set; }

    /// <summary>Tempo previsto do item atual; nulo na espera.</summary>
    public int? DuracaoItemSegundos { get; set; }

    public long? MidiaAtualId { get; set; }

    /// <summary>O app está em primeiro plano.</summary>
    public bool? PrimeiroPlano { get; set; }

    /// <summary>Anúncios que a TV tem para tocar hoje (filtrados e baixados).</summary>
    public int? ItensHoje { get; set; }

    /// <summary>Relógio da TV menos o do servidor, em segundos (positivo = TV adiantada).</summary>
    public int? DesvioRelogioSegundos { get; set; }

    /// <summary>Desde quando a TV sincroniza mas não exibe; nulo = exibindo (ou app sem heartbeat).</summary>
    public DateTimeOffset? ForaDeExibicaoDesde { get; set; }

    public MotivoForaDeExibicao? MotivoForaDeExibicao { get; set; }

    /// <summary>Quando a operadora foi avisada de que a TV parou de exibir; limpo ao voltar.</summary>
    public DateTimeOffset? AlertaForaDeExibicaoEm { get; set; }

    public bool RelogioErrado => DesvioRelogioSegundos is { } d && Math.Abs(d) > ToleranciaRelogio.TotalSeconds;

    /// <summary>
    /// Grava o que a TV informou e decide se ela está exibindo. O tempo do item
    /// é medido pelo relógio da própria TV (início e "agora" dela), então um
    /// relógio errado não cria um falso travamento.
    /// </summary>
    public void RegistrarReproducao(
        DateTimeOffset agora,
        ModoReproducao modo,
        DateTimeOffset desde,
        int? duracaoSegundos,
        long? midiaId,
        bool primeiroPlano,
        int itensHoje,
        DateTimeOffset relogioTv)
    {
        ModoReproducao = modo;
        ReproducaoDesde = desde;
        DuracaoItemSegundos = duracaoSegundos;
        MidiaAtualId = midiaId;
        PrimeiroPlano = primeiroPlano;
        ItensHoje = itensHoje;
        DesvioRelogioSegundos = (int)Math.Round((relogioTv - agora).TotalSeconds);

        var naTela = relogioTv - desde;
        MotivoForaDeExibicao? motivo =
            !primeiroPlano ? Enums.MotivoForaDeExibicao.AppForaDaTela
            : modo != Enums.ModoReproducao.Espera && naTela > TimeSpan.FromSeconds(duracaoSegundos ?? 0) + FolgaTravamento
                ? Enums.MotivoForaDeExibicao.ReproducaoTravada
            : modo == Enums.ModoReproducao.Espera && itensHoje > 0 ? Enums.MotivoForaDeExibicao.EsperaComAnuncios
            : null;

        if (motivo is null)
        {
            ForaDeExibicaoDesde = null;
            MotivoForaDeExibicao = null;
        }
        else
        {
            ForaDeExibicaoDesde ??= agora;
            MotivoForaDeExibicao = motivo;
        }
    }

    public void RegistrarSincronizacao(DateTimeOffset agora, string? versaoApp, string versaoGrade, int? itensProntos)
    {
        UltimaSincronizacao = agora;
        VersaoApp = versaoApp;
        VersaoGrade = versaoGrade;
        ItensProntos = itensProntos;
    }
}
