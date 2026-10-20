namespace Atlas.Application.Business.Common;

/// <summary>
/// Configurações do produto que não são segredo. As operacionais (alertas,
/// teto, intervalos) vêm de Atlas Studio → Parâmetros, grupo "Mídia Indoor"
/// (ADR-026); as chaves <c>Indoor:…</c> citadas abaixo valem só se o parâmetro
/// não existir. Endereços vêm da seção <c>Indoor</c> do appsettings.
/// </summary>
public interface IConfiguracaoIndoor
{
    /// <summary>Endereço do painel (pasta <c>web/</c>), usado nos e-mails.</summary>
    string UrlPainel { get; }

    /// <summary>
    /// Endereço público da API na internet (<c>Indoor:UrlPublica</c>, ex.:
    /// <c>https://tv.minharede.com.br</c>), base do diretório das TVs
    /// (<c>/d/{codigo}</c>). Nulo = diretório indisponível: as TVs não mostram o QR.
    /// </summary>
    Uri? UrlPublica { get; }

    /// <summary>
    /// E-mails da operadora que recebem os avisos (troca de arquivo para
    /// aprovar, TV fora do ar): <c>Indoor:Alertas:Emails</c>. Vazio = sem e-mail.
    /// </summary>
    IReadOnlyList<string> EmailsOperadora { get; }

    /// <summary>Sem sincronizar há mais que isto, a TV gera alerta (<c>Indoor:Alertas:TvOfflineMinutos</c>, padrão 15).</summary>
    TimeSpan ToleranciaAlertaOffline { get; }

    /// <summary>
    /// Hora do dia (no fuso do estabelecimento) em que os alertas de TV fora do
    /// ar valem: <c>Indoor:Alertas:HoraInicio</c> (padrão 7) até
    /// <c>Indoor:Alertas:HoraFim</c> (padrão 22). Fora disso a loja pode estar fechada.
    /// </summary>
    (int Inicio, int Fim) HorarioAlertas { get; }

    /// <summary>
    /// Teto de armazenamento da rede inteira (<c>Indoor:Armazenamento:LimiteTotalMb</c>,
    /// padrão 9.216 MB = 9 GB, máximo 10.240 MB): os 10 GB grátis do R2 com folga.
    /// Nenhum envio é liberado se ultrapassar (ADR-025).
    /// </summary>
    long LimiteArmazenamentoBytes { get; }

    /// <summary>De quanto em quanto tempo a TV consulta a grade (<c>Indoor:Dispositivo:IntervaloSincronizacaoSegundos</c>, padrão 60).</summary>
    TimeSpan IntervaloSincronizacao { get; }

    /// <summary>Validade do token da TV (<c>Indoor:Dispositivo:ValidadeTokenDias</c>, padrão 30). Renovado sozinho na sincronização.</summary>
    TimeSpan ValidadeTokenDispositivo { get; }

    /// <summary>Validade das URLs de download entregues às TVs (<c>Indoor:Dispositivo:ValidadeUrlDownloadHoras</c>, padrão 24).</summary>
    TimeSpan ValidadeUrlDownload { get; }

    /// <summary>Liga as coletas em segundo plano de notícias e clima (<c>Indoor:Coleta:Ativa</c>, padrão true).</summary>
    bool ColetaAutomatica { get; }

    /// <summary>De quanto em quanto tempo os feeds de notícias são lidos (<c>Indoor:Coleta:NoticiasMinutos</c>, padrão 60).</summary>
    TimeSpan IntervaloColetaNoticias { get; }

    /// <summary>De quanto em quanto tempo o clima é atualizado (<c>Indoor:Coleta:ClimaMinutos</c>, padrão 30).</summary>
    TimeSpan IntervaloColetaClima { get; }

    /// <summary>De quanto em quanto tempo as cotações são atualizadas (<c>Indoor:Coleta:CotacoesMinutos</c>, padrão 30).</summary>
    TimeSpan IntervaloColetaCotacoes { get; }

    /// <summary>Serviço PTAX (OData) do Banco Central (<c>Indoor:Cotacoes:UrlPtax</c>).</summary>
    Uri UrlPtax { get; }

    /// <summary>API de previsão do Open-Meteo (<c>Indoor:Clima:UrlPrevisao</c>).</summary>
    Uri UrlPrevisaoClima { get; }

    /// <summary>API de geocodificação do Open-Meteo, para achar a cidade (<c>Indoor:Clima:UrlGeocodificacao</c>).</summary>
    Uri UrlGeocodificacao { get; }
}
