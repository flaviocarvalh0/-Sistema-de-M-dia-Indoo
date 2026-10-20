using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Atlas.Infrastructure.Business.Parametros;

/// <summary>Um parâmetro do produto em <c>platform.system_parameter</c> (Atlas Studio → Parâmetros).</summary>
/// <param name="Chave">Chave na tabela (maiúsculas, como a plataforma grava).</param>
/// <param name="ChaveConfiguracao">Onde o valor ficava no appsettings: vira o valor inicial do parâmetro.</param>
/// <param name="Tipo">STRING, NUMBER, BOOLEAN ou JSON (os tipos da tela de Parâmetros).</param>
/// <param name="Padrao">Valor inicial quando a configuração também não tem.</param>
internal sealed record DefinicaoParametro(string Chave, string ChaveConfiguracao, string Tipo, string Padrao, string Descricao);

/// <summary>
/// Parâmetros operacionais da Mídia Indoor (ADR-026): o que a operadora ajusta
/// pelo painel, sem reiniciar a API e sem User Secrets. Segredos (banco, JWT,
/// chaves do storage) continuam fora do banco (ADR-008).
/// </summary>
/// <remarks>
/// Singleton com uma cópia em memória, atualizada pelo
/// <see cref="SincronizadorParametrosIndoor"/> a cada minuto: as propriedades de
/// <c>IConfiguracaoIndoor</c> são lidas a todo instante e não podem ir ao banco.
/// </remarks>
internal sealed class ParametrosIndoor
{
    public const string Grupo = "Mídia Indoor";

    public static readonly DefinicaoParametro AlertasEmails = new(
        "INDOOR.ALERTAS.EMAILS", "Indoor:Alertas:Emails", "JSON", "[]",
        "E-mails da operadora que recebem os avisos (TV fora do ar, troca de arquivo para aprovar, armazenamento). Lista JSON, ex.: [\"operadora@empresa.com\"].");

    public static readonly DefinicaoParametro AlertasTvOfflineMinutos = new(
        "INDOOR.ALERTAS.TV_OFFLINE_MINUTOS", "Indoor:Alertas:TvOfflineMinutos", "NUMBER", "15",
        "Minutos sem sincronizar até o alerta de TV fora do ar (mínimo 5).");

    public static readonly DefinicaoParametro AlertasHoraInicio = new(
        "INDOOR.ALERTAS.HORA_INICIO", "Indoor:Alertas:HoraInicio", "NUMBER", "7",
        "Hora (0 a 23, no fuso do local da TV) a partir da qual os alertas de TV valem.");

    public static readonly DefinicaoParametro AlertasHoraFim = new(
        "INDOOR.ALERTAS.HORA_FIM", "Indoor:Alertas:HoraFim", "NUMBER", "22",
        "Hora (1 a 24, no fuso do local da TV) em que os alertas de TV param.");

    public static readonly DefinicaoParametro ArmazenamentoLimiteMb = new(
        "INDOOR.ARMAZENAMENTO.LIMITE_TOTAL_MB", "Indoor:Armazenamento:LimiteTotalMb", "NUMBER", "9216",
        "Teto do storage da rede inteira, em MB. Acima de 10240 (10 GB grátis do R2) vale 10240. Envio que não couber é recusado.");

    public static readonly DefinicaoParametro DispositivoIntervaloSegundos = new(
        "INDOOR.DISPOSITIVO.INTERVALO_SINCRONIZACAO_SEGUNDOS", "Indoor:Dispositivo:IntervaloSincronizacaoSegundos", "NUMBER", "60",
        "De quantos em quantos segundos a TV consulta a grade (mínimo 30).");

    public static readonly DefinicaoParametro DispositivoValidadeTokenDias = new(
        "INDOOR.DISPOSITIVO.VALIDADE_TOKEN_DIAS", "Indoor:Dispositivo:ValidadeTokenDias", "NUMBER", "30",
        "Validade do token da TV, em dias. É renovado sozinho na sincronização.");

    public static readonly DefinicaoParametro DispositivoValidadeUrlHoras = new(
        "INDOOR.DISPOSITIVO.VALIDADE_URL_DOWNLOAD_HORAS", "Indoor:Dispositivo:ValidadeUrlDownloadHoras", "NUMBER", "24",
        "Validade dos links de download entregues às TVs, em horas.");

    public static readonly DefinicaoParametro ColetaAtiva = new(
        "INDOOR.COLETA.ATIVA", "Indoor:Coleta:Ativa", "BOOLEAN", "true",
        "Liga a coleta automática de notícias, clima e cotações.");

    public static readonly DefinicaoParametro ColetaNoticiasMinutos = new(
        "INDOOR.COLETA.NOTICIAS_MINUTOS", "Indoor:Coleta:NoticiasMinutos", "NUMBER", "60",
        "De quantos em quantos minutos os feeds de notícias são lidos (mínimo 15).");

    public static readonly DefinicaoParametro ColetaClimaMinutos = new(
        "INDOOR.COLETA.CLIMA_MINUTOS", "Indoor:Coleta:ClimaMinutos", "NUMBER", "30",
        "De quantos em quantos minutos o clima é atualizado (mínimo 15).");

    public static readonly DefinicaoParametro ColetaCotacoesMinutos = new(
        "INDOOR.COLETA.COTACOES_MINUTOS", "Indoor:Coleta:CotacoesMinutos", "NUMBER", "30",
        "De quantos em quantos minutos as cotações (PTAX) são atualizadas (mínimo 15).");

    public static readonly DefinicaoParametro UrlPublica = new(
        "INDOOR.URL_PUBLICA", "Indoor:UrlPublica", "STRING", "",
        "Endereço HTTPS da API na internet, que o celular abre ao ler o QR da TV (ex.: https://tv.suaempresa.com.br). Base do QR rastreado (/q) e do diretório (/d). Vazio ou localhost: QR direto ao destino, sem contar leituras, e sem QR do diretório.");

    public static readonly DefinicaoParametro AnuncieWhatsApp = new(
        "INDOOR.ANUNCIE.WHATSAPP", "Indoor:Anuncie:WhatsApp", "STRING", "",
        "WhatsApp da operadora para quem quer anunciar (DDD e número, ex.: 92999990000). Preenchido: a TV convida \"Quer anunciar aqui?\" e a página do diretório ganha o botão. Vazio: sem convite.");

    public static readonly IReadOnlyList<DefinicaoParametro> Todos =
    [
        UrlPublica, AnuncieWhatsApp,
        AlertasEmails, AlertasTvOfflineMinutos, AlertasHoraInicio, AlertasHoraFim,
        ArmazenamentoLimiteMb,
        DispositivoIntervaloSegundos, DispositivoValidadeTokenDias, DispositivoValidadeUrlHoras,
        ColetaAtiva, ColetaNoticiasMinutos, ColetaClimaMinutos, ColetaCotacoesMinutos,
    ];

    private volatile FrozenDictionary<string, string> _valores = FrozenDictionary<string, string>.Empty;

    /// <summary>Quando a cópia foi lida do banco pela última vez (nulo: ainda não leu).</summary>
    public DateTimeOffset? AtualizadoEm { get; private set; }

    public void Atualizar(IEnumerable<KeyValuePair<string, string>> valores, DateTimeOffset agora)
    {
        _valores = valores.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        AtualizadoEm = agora;
    }

    /// <summary>O valor cadastrado, ou nulo se não houver (aí vale a configuração).</summary>
    public string? Valor(DefinicaoParametro definicao) =>
        _valores.TryGetValue(definicao.Chave, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor.Trim() : null;

    public int? Inteiro(DefinicaoParametro definicao) =>
        decimal.TryParse(Valor(definicao), NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            ? (int)Math.Round(numero)
            : null;

    public bool? Logico(DefinicaoParametro definicao) =>
        bool.TryParse(Valor(definicao), out var logico) ? logico : null;

    /// <summary>Lista JSON de textos. Aceita também e-mails separados por vírgula ou ponto e vírgula.</summary>
    public IReadOnlyList<string>? Lista(DefinicaoParametro definicao)
    {
        var valor = Valor(definicao);
        if (valor is null)
        {
            return null;
        }

        if (valor.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(valor) ?? [];
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return valor.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
