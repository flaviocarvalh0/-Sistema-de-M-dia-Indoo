using Atlas.Application.Business.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Infrastructure.Business.Parametros;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business;

/// <summary>
/// Configurações da Mídia Indoor. O que é operacional vem primeiro dos
/// parâmetros cadastrados (Atlas Studio → Parâmetros, ADR-026); sem eles, da
/// seção <c>Indoor</c> do appsettings; sem ela, do padrão. Endereços e
/// segredos ficam só na configuração.
/// </summary>
internal sealed class ConfiguracaoIndoor(IConfiguration configuration, ParametrosIndoor? parametros = null) : IConfiguracaoIndoor
{
    public string UrlPainel =>
        configuration["Indoor:UrlPainel"]
        ?? throw new InvalidOperationException("Configuração 'Indoor:UrlPainel' ausente no appsettings.");

    /// <summary>
    /// Nulo também para <c>localhost</c>: o celular que lê o QR abriria a si
    /// mesmo, e a leitura nunca chegaria à API (o QR vai direto ao destino).
    /// </summary>
    public Uri? UrlPublica =>
        Uri.TryCreate(parametros?.Valor(ParametrosIndoor.UrlPublica) ?? configuration["Indoor:UrlPublica"], UriKind.Absolute, out var url)
        && url.Scheme is "http" or "https"
        && !url.IsLoopback
            ? url
            : null;

    public string? WhatsAppAnuncie =>
        MontadorQrCode.NormalizarTelefone(parametros?.Valor(ParametrosIndoor.AnuncieWhatsApp) ?? configuration["Indoor:Anuncie:WhatsApp"] ?? string.Empty);

    public IReadOnlyList<string> EmailsOperadora =>
        (parametros?.Lista(ParametrosIndoor.AlertasEmails)
            ?? configuration.GetSection("Indoor:Alertas:Emails").Get<string[]>()
            ?? [])
            .Select(e => e.Trim())
            .Where(e => e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Nunca acima de 10 GB (o grátis do R2), mesmo que a configuração peça.</summary>
    public long LimiteArmazenamentoBytes =>
        Math.Clamp(Inteiro(ParametrosIndoor.ArmazenamentoLimiteMb, padrao: 9216, minimo: 100), 100, 10240) * 1024L * 1024L;

    public TimeSpan ToleranciaAlertaOffline =>
        TimeSpan.FromMinutes(Inteiro(ParametrosIndoor.AlertasTvOfflineMinutos, padrao: 15, minimo: 5));

    public (int Inicio, int Fim) HorarioAlertas =>
        (Math.Clamp(Inteiro(ParametrosIndoor.AlertasHoraInicio, padrao: 7, minimo: 0), 0, 23),
         Math.Clamp(Inteiro(ParametrosIndoor.AlertasHoraFim, padrao: 22, minimo: 1), 1, 24));

    public TimeSpan IntervaloSincronizacao =>
        TimeSpan.FromSeconds(Inteiro(ParametrosIndoor.DispositivoIntervaloSegundos, padrao: 60, minimo: 30));

    public TimeSpan ValidadeTokenDispositivo =>
        TimeSpan.FromDays(Inteiro(ParametrosIndoor.DispositivoValidadeTokenDias, padrao: 30, minimo: 1));

    public TimeSpan ValidadeUrlDownload =>
        TimeSpan.FromHours(Inteiro(ParametrosIndoor.DispositivoValidadeUrlHoras, padrao: 24, minimo: 1));

    public bool ColetaAutomatica =>
        parametros?.Logico(ParametrosIndoor.ColetaAtiva)
        ?? (!bool.TryParse(configuration[ParametrosIndoor.ColetaAtiva.ChaveConfiguracao], out var ativa) || ativa);

    public TimeSpan IntervaloColetaNoticias =>
        TimeSpan.FromMinutes(Inteiro(ParametrosIndoor.ColetaNoticiasMinutos, padrao: 60, minimo: 15));

    public TimeSpan IntervaloColetaClima =>
        TimeSpan.FromMinutes(Inteiro(ParametrosIndoor.ColetaClimaMinutos, padrao: 30, minimo: 15));

    public TimeSpan IntervaloColetaCotacoes =>
        TimeSpan.FromMinutes(Inteiro(ParametrosIndoor.ColetaCotacoesMinutos, padrao: 30, minimo: 15));

    public Uri UrlPtax =>
        new(configuration["Indoor:Cotacoes:UrlPtax"] ?? "https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata");

    public Uri UrlPrevisaoClima =>
        new(configuration["Indoor:Clima:UrlPrevisao"] ?? "https://api.open-meteo.com/v1/forecast");

    public Uri UrlGeocodificacao =>
        new(configuration["Indoor:Clima:UrlGeocodificacao"] ?? "https://geocoding-api.open-meteo.com/v1/search");

    /// <summary>Parâmetro cadastrado, senão a configuração, senão o padrão; nunca abaixo do mínimo.</summary>
    private int Inteiro(DefinicaoParametro definicao, int padrao, int minimo)
    {
        var valor = parametros?.Inteiro(definicao)
            ?? (int.TryParse(configuration[definicao.ChaveConfiguracao], out var configurado) ? configurado : padrao);
        return Math.Max(valor, minimo);
    }
}
