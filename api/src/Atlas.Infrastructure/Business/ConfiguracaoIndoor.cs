using Atlas.Application.Business.Common;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Business;

/// <summary>Lê a seção <c>Indoor</c> do appsettings.</summary>
internal sealed class ConfiguracaoIndoor(IConfiguration configuration) : IConfiguracaoIndoor
{
    public string UrlPainel =>
        configuration["Indoor:UrlPainel"]
        ?? throw new InvalidOperationException("Configuração 'Indoor:UrlPainel' ausente no appsettings.");

    public TimeSpan IntervaloSincronizacao =>
        TimeSpan.FromSeconds(Inteiro("Indoor:Dispositivo:IntervaloSincronizacaoSegundos", padrao: 60, minimo: 30));

    public TimeSpan ValidadeTokenDispositivo =>
        TimeSpan.FromDays(Inteiro("Indoor:Dispositivo:ValidadeTokenDias", padrao: 30, minimo: 1));

    public TimeSpan ValidadeUrlDownload =>
        TimeSpan.FromHours(Inteiro("Indoor:Dispositivo:ValidadeUrlDownloadHoras", padrao: 24, minimo: 1));

    public bool ColetaAutomatica =>
        !bool.TryParse(configuration["Indoor:Coleta:Ativa"], out var ativa) || ativa;

    public TimeSpan IntervaloColetaNoticias =>
        TimeSpan.FromMinutes(Inteiro("Indoor:Coleta:NoticiasMinutos", padrao: 60, minimo: 15));

    public TimeSpan IntervaloColetaClima =>
        TimeSpan.FromMinutes(Inteiro("Indoor:Coleta:ClimaMinutos", padrao: 30, minimo: 15));

    public Uri UrlPrevisaoClima =>
        new(configuration["Indoor:Clima:UrlPrevisao"] ?? "https://api.open-meteo.com/v1/forecast");

    public Uri UrlGeocodificacao =>
        new(configuration["Indoor:Clima:UrlGeocodificacao"] ?? "https://geocoding-api.open-meteo.com/v1/search");

    private int Inteiro(string chave, int padrao, int minimo) =>
        int.TryParse(configuration[chave], out var valor) ? Math.Max(valor, minimo) : padrao;
}
